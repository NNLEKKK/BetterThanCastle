using System;
using HarmonyLib;

namespace BetterThanCastle
{
    /// <summary>In-game chat box (ChatBoxDriver): record arrival times of lines (BtcGameChatFade).</summary>
    [HarmonyPatch(typeof(ChatBoxDriver), "ReceivedChatMessage")]
    internal static class ChatBoxReceivedPatch
    {
        private static void Postfix(ChatBoxDriver __instance, string msg)
        {
            try { BtcGameChatFade.Add(__instance, msg); }
            catch (Exception e) { BtcLog.Error("Hook", "ChatBoxDriver.ReceivedChatMessage: " + e); }
        }
    }

    /// <summary>ChatBoxDriver.Initialize: new chat session (intro line is kept as the first line).</summary>
    [HarmonyPatch(typeof(ChatBoxDriver), "Initialize")]
    internal static class ChatBoxInitializePatch
    {
        private static void Postfix(ChatBoxDriver __instance)
        {
            try { BtcGameChatFade.Reset(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ChatBoxDriver.Initialize: " + e); }
        }
    }
}
