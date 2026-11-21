#if DEV
using System;
using Brix.Lifecycle.Pooling;
using Brix.External.Factories;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: pool releases the pool refuses during replay playback (already released / release loop) - the object then stays in
    /// the scene; logs name, netId, registered flag and whether a seek is applying.</summary>
    [HarmonyPatch(typeof(ObjectPoolSingleton), "Release", new Type[] { typeof(FactoryImprint) })]
    internal static class BtcDevPoolProbe
    {
        private static int _n;

        private static void Prefix(FactoryImprint imprint)
        {
            try
            {
                if (!BtcReplay.Running || imprint == null || !imprint.Released || _n >= 60) return;
                _n++;
                NetworkIdentity ni = imprint.GetComponent<NetworkIdentity>();
                BtcLog.Info("PoolProbe", "release refused (already released): " + imprint.name + (ni != null ? " #" + ni.netId.Value : "") + " active " + imprint.gameObject.activeInHierarchy
                    + " registered " + (ni != null && ClientScene.FindLocalObject(ni.netId) == imprint.gameObject) + " stack " + System.Environment.StackTrace.Replace("\n", " | ").Substring(0, 700));
            }
            catch (Exception) { }
        }
    }
}
#endif
