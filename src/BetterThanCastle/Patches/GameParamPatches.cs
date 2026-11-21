using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Brix.Assets;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>GameParam.Map setter: BtcSpectate.KeepMap blocks map changes; map checksum via BtcLoadBoost.MapChecksum (cached hash).</summary>
    [HarmonyPatch(typeof(GameParam), "Map", MethodType.Setter)]
    internal static class GameParamSetMapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Asset_Map value)
        {
            try { return !BtcSpectate.KeepMap(value); }
            catch (Exception e) { BtcLog.Error("Hook", "GameParam.set_Map: " + e); return true; }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            // vanilla: NetworkmapChecksum = Legitimizer.GenerateOneHashForAllFilesIn(value.path).hash;
            try
            {
                CodeMatcher cm = new CodeMatcher(src);
                cm.MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is System.Reflection.MethodInfo && ((System.Reflection.MethodInfo)i.operand).Name == "GenerateOneHashForAllFilesIn"),
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is System.Reflection.FieldInfo && ((System.Reflection.FieldInfo)i.operand).Name == "hash"));
                if (cm.IsInvalid)
                {
                    BtcLog.Error("Hook", "GameParam.set_Map: GenerateOneHashForAllFilesIn pattern not found");
                    return src;
                }
                cm.SetOperandAndAdvance(AccessTools.Method(typeof(BtcLoadBoost), "MapChecksum"));
                cm.SetOpcodeAndAdvance(OpCodes.Nop);
                return cm.InstructionEnumeration();
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "GameParam.set_Map transpiler: " + e);
                return src;
            }
        }
    }

    /// <summary>Max factions cap (spectator slot): BtcLobby.AdjCap.</summary>
    [HarmonyPatch(typeof(GameParam), "GetMaxFactions")]
    internal static class GameParamGetMaxFactionsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            try { __result = BtcLobby.AdjCap(__result); }
            catch (Exception e) { BtcLog.Error("Hook", "GameParam.GetMaxFactions: " + e); }
        }
    }

    /// <summary>RefreshFactions: BtcLobby.OnFactionsRefreshed at the end (not on the vanilla early returns).</summary>
    [HarmonyPatch(typeof(GameParam), "RefreshFactions")]
    internal static class GameParamRefreshFactionsPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                Asset_Map map = GameParam.Map;
                if (map == null) return;
                if (map.IsNewGame() && GameParam.Gamemode == null) return;
                BtcLobby.OnFactionsRefreshed();
            }
            catch (Exception e) { BtcLog.Error("Hook", "GameParam.RefreshFactions: " + e); }
        }
    }
}
