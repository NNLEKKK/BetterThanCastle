using Brix.Game.AI.Damage;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// BC ON: HP / stamina / block HP SyncVars go out on the next server update instead of waiting for the 0.1 s send interval
    /// (same bytes, only sooner). Damage events (SyncEvent) and deaths (RpcDie) were already immediate.
    /// </summary>
    [HarmonyPatch(typeof(NetworkBehaviour), "GetNetworkSendInterval")]
    internal static class NetworkBehaviourSendIntervalPatch
    {
        private static void Postfix(NetworkBehaviour __instance, ref float __result)
        {
            if (__instance is DamageReceiver && BtcNetworkGate.Enabled) __result = 0f;
        }
    }
}
