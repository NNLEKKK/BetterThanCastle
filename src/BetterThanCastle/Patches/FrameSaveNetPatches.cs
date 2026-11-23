using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// Game (UNET HLAPI): NetworkIdentity.observers built a new ReadOnlyCollection over the same list on every call, and
    /// NetworkServer.SendBytesToReady calls it for every networked object on every update (13 KB/s of garbage in a busy match).
    /// Saver: one wrapper per observer list, made again only when the identity gets a new list. Same connections in the same
    /// order (the wrapper reads the live list), so nothing changes on the wire, also with BetterConnection off.
    [HarmonyPatch(typeof(NetworkIdentity), "observers", MethodType.Getter)]
    internal static class FrameSaveNetObservers
    {
        private struct Entry { internal List<NetworkConnection> List; internal ReadOnlyCollection<NetworkConnection> Wrap; }

        private static AccessTools.FieldRef<NetworkIdentity, List<NetworkConnection>> _list;
        private static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>();
        private static int _scene = int.MinValue;

        static bool Prepare()
        {
            if (!BtcFrameSave.On) return false;
            if (AccessTools.Field(typeof(NetworkIdentity), "m_Observers") == null) { BtcLog.Warn("FrameSave", "NetworkIdentity.m_Observers not found: observers saver off"); return false; }
            _list = AccessTools.FieldRefAccess<NetworkIdentity, List<NetworkConnection>>("m_Observers");
            return true;
        }

        static bool Prefix(NetworkIdentity __instance, ref ReadOnlyCollection<NetworkConnection> __result)
        {
            try
            {
                List<NetworkConnection> l = _list(__instance);
                if (l == null) { __result = null; return false; }
                int s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
                if (s != _scene) { _scene = s; Cache.Clear(); }
                int id = __instance.GetInstanceID();
                Entry e;
                if (!Cache.TryGetValue(id, out e) || !ReferenceEquals(e.List, l))
                {
                    e = new Entry { List = l, Wrap = new ReadOnlyCollection<NetworkConnection>(l) };
                    Cache[id] = e;
                }
                __result = e.Wrap;
                return false;
            }
            catch (Exception ex) { BtcLog.Error("FrameSave", "observers: " + ex.Message); return true; }
        }
    }
}
