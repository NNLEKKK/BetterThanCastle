using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brix.UI.Builder.Menu;
using Brix.UI.Scripts;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// GameParam.CurrentTeamInfos is `static readonly` with 4 entries; swapping it by reflection is unreliable (callers JIT-ed earlier may keep the old
    /// array, UI never refreshes) which made teams E-H appear only "sometimes". Instead every reader (GameParam, LegacyLobbyLeftMenu,
    /// NetworkPlayersMenu incl. closures) is transpiled: ldsfld CurrentTeamInfos -> call BtcLobby.TeamInfos() (8 entries only for BC ON + multiplayer).
    /// </summary>
    [HarmonyPatch]
    internal static class LobbyTeamInfosPatch
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static FieldInfo _field;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> res = new List<MethodBase>();
            try
            {
                _field = typeof(GameParam).GetField("CurrentTeamInfos", All);
                if (_field == null) { BtcLog.Error("Hook", "GameParam.CurrentTeamInfos not found"); return res; }
                byte[] tok = BitConverter.GetBytes(_field.MetadataToken);
                Type[] roots = { typeof(GameParam), typeof(LegacyLobbyLeftMenu), typeof(NetworkPlayersMenu) };
                for (int i = 0; i < roots.Length; i++) Scan(roots[i], tok, res);
                BtcLog.Info("Hook", "CurrentTeamInfos readers: " + res.Count);
            }
            catch (Exception e) { BtcLog.Error("Hook", "LobbyTeamInfos TargetMethods: " + e); }
            return res;
        }

        private static void Scan(Type t, byte[] tok, List<MethodBase> res)
        {
            foreach (MethodBase m in t.GetMethods(All)) Check(m, tok, res);
            foreach (MethodBase m in t.GetConstructors(All)) Check(m, tok, res);
            foreach (Type n in t.GetNestedTypes(All)) Scan(n, tok, res);
        }

        private static void Check(MethodBase m, byte[] tok, List<MethodBase> res)
        {
            try
            {
                if (m.IsAbstract || m.ContainsGenericParameters) return;
                MethodBody body = m.GetMethodBody();
                byte[] il = body == null ? null : body.GetILAsByteArray();
                if (il == null) return;
                for (int i = 1; i + tok.Length <= il.Length; i++)
                {
                    if (il[i - 1] != 0x7E) continue; // ldsfld
                    int k = 0;
                    while (k < tok.Length && il[i + k] == tok[k]) k++;
                    if (k == tok.Length) { res.Add(m); return; }
                }
            }
            catch (Exception) { }
        }

        private static bool Prepare() { return ((List<MethodBase>)TargetMethods()).Count > 0; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src, MethodBase __originalMethod)
        {
            try
            {
                List<CodeInstruction> list = new List<CodeInstruction>(src);
                MethodInfo repl = AccessTools.Method(typeof(BtcLobby), "TeamInfos");
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    FieldInfo f = list[i].operand as FieldInfo;
                    if (list[i].opcode == OpCodes.Ldsfld && f != null && f.Name == "CurrentTeamInfos" && f.DeclaringType == typeof(GameParam))
                    {
                        list[i].opcode = OpCodes.Call;
                        list[i].operand = repl;
                        n++;
                    }
                }
                if (n == 0) BtcLog.Error("Hook", "CurrentTeamInfos ldsfld not found in " + __originalMethod);
                return list;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "LobbyTeamInfos transpiler: " + e);
                return src;
            }
        }
    }

    /// <summary>NetworkPlayersMenu.GetPlayerBuilders hard-codes 4 teams x 4 players: BC ON builds 8 x 8 (unused panels stay inactive).</summary>
    [HarmonyPatch(typeof(NetworkPlayersMenu), "GetPlayerBuilders")]
    internal static class NetworkPlayersMenuBuildersPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> src)
        {
            try
            {
                List<CodeInstruction> list = new List<CodeInstruction>(src);
                List<int> idx = new List<int>();
                for (int i = 0; i < list.Count; i++)
                    if (list[i].opcode == OpCodes.Ldc_I4_4) idx.Add(i);
                if (idx.Count != 2)
                {
                    BtcLog.Error("Hook", "NetworkPlayersMenu.GetPlayerBuilders: expected 2 x ldc.i4.4, found " + idx.Count);
                    return src;
                }
                list[idx[0]] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcLobby), "NetTeams"));
                list[idx[1]] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BtcLobby), "NetSlots"));
                return list;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "NetworkPlayersMenu.GetPlayerBuilders transpiler: " + e);
                return src;
            }
        }
    }
}
