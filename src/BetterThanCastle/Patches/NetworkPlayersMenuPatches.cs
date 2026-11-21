using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Network;
using Brix.UI.Builder.Menu;
using Brix.UI.Builder.Panel;
using Brix.Utils;
using Brix.Utils.Collections;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// GetPlayerPanel's onRefresh lambda: after vanilla fills the panel, bind the BtcPlate and prefer the BtcAvatar texture.
    /// The lambda lives in a compiler generated closure class (captures teamIndex/playerIndex); found by its call to GetReadyStatus.
    /// </summary>
    [HarmonyPatch]
    internal static class NetworkPlayersMenuOnRefreshPatch
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static FieldInfo _team, _player;

        private static MethodBase TargetMethod()
        {
            MethodInfo ready = AccessTools.Method(typeof(NetworkPlayersMenu), "GetReadyStatus");
            if (ready == null) { BtcLog.Error("Hook", "NetworkPlayersMenu.GetReadyStatus not found"); return null; }
            byte[] tok = BitConverter.GetBytes(ready.MetadataToken);
            foreach (Type t in typeof(NetworkPlayersMenu).GetNestedTypes(All))
            {
                foreach (MethodInfo m in t.GetMethods(All))
                {
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(ThumbnailPanelComponent) || m.IsStatic) continue;
                    MethodBody body = m.GetMethodBody();
                    byte[] il = body == null ? null : body.GetILAsByteArray();
                    if (il == null || IndexOf(il, tok) < 0) continue;
                    _team = t.GetField("teamIndex", All);
                    _player = t.GetField("playerIndex", All);
                    if (_team == null || _player == null) { BtcLog.Error("Hook", "NetworkPlayersMenu closure fields not found in " + t.Name); return null; }
                    return m;
                }
            }
            // lambdas without captures would be static on the outer class; this one captures indices, so give up
            BtcLog.Error("Hook", "NetworkPlayersMenu.GetPlayerPanel onRefresh lambda not found");
            return null;
        }

        private static bool Prepare() { return TargetMethod() != null; }

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int k = 0;
                while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return i;
            }
            return -1;
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, ThumbnailPanelComponent __0)
        {
            try
            {
                ThumbnailPanelComponent panel = __0;
                if (panel == null || !panel.gameObject.activeSelf) return;
                int team = (int)_team.GetValue(__instance);
                int idx = (int)_player.GetValue(__instance);
                List<LobbyPlayer> list = GameParam.GetTeamPlayers(team, CollectionPool.RequestList<LobbyPlayer>());
                LobbyPlayer p = idx < list.Count ? list[idx] : null;
                list.Dispose<LobbyPlayer>();
                if (p == null) return;
                bool local = LobbyMenu.Player.Equals(p);
                BtcPlate.Bind(panel._bgImage, panel._avatarBgImage, p, local);
                Texture tex = BtcAvatar.For(p);
                if (tex != null)
                {
                    panel._avatarRawImage.texture = tex;
                    panel._avatarRawImage.gameObject.SetActive(true);
                    panel._avatarGo.SetActive(true);
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "NetworkPlayersMenu.onRefresh: " + e); }
        }
    }
}
