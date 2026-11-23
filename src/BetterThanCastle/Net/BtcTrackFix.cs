using System;
using System.Collections.Generic;
using Brix.Game.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Late parent links (local fix, no wire change). An item's holder (unit toolbag, rack, stockpile, carrier) travels as
    /// GenericParentTracker.currentTracker = TrackingPair{GameObject, type}; the weaver reads the GameObject at once
    /// (reader.ReadGameObject), so when the item's spawn arrives before its holder's (hot-join/rejoin spawns in UNET dictionary
    /// order, pooled objects reuse free slots; replay seeks) the link becomes null for good: the kit lies in the world, the rack
    /// looks empty, until the next real change (re-equip by hand). Here the netId is kept and the link is applied as soon as the
    /// holder exists, the same way a normal tracker update applies it.
    /// </summary>
    internal static class BtcTrackFix
    {
        private struct Pending
        {
            public NetworkInstanceId Holder;
            public int Type;
            public uint Self;
            public float Until;
        }

        private const float GiveUp = 120f;
        [ThreadStatic] internal static GenericParentTracker Current;
        private static readonly Dictionary<GenericParentTracker, Pending> Waiting = new Dictionary<GenericParentTracker, Pending>();
        private static readonly List<GenericParentTracker> _done = new List<GenericParentTracker>();
        private static readonly AccessTools.FieldRef<GenericParentTracker, TrackingPair> CurRef = AccessTools.FieldRefAccess<GenericParentTracker, TrackingPair>("currentTracker");
        private static readonly Action<GenericParentTracker, TrackingPair> Changed =
            AccessTools.MethodDelegate<Action<GenericParentTracker, TrackingPair>>(AccessTools.Method(typeof(GenericParentTracker), "CurrentTrackerChanged"));
        private static int _fixed, _lost;
        private static BtcTrackFixRunner _runner;

        internal static bool On { get { return BtcNetworkGate.Enabled || BtcReplay.Active; } }

        /// <summary>Replaces GeneratedNetworkCode._ReadTrackingPair_None inside GenericParentTracker.OnDeserialize (same bytes read).</summary>
        internal static TrackingPair Read(NetworkReader reader)
        {
            NetworkInstanceId id = reader.ReadNetworkId();
            int type = (int)reader.ReadPackedUInt32();
            GameObject go = null;
            if (!id.IsEmpty()) go = NetworkServer.active ? NetworkServer.FindLocalObject(id) : ClientScene.FindLocalObject(id);
            GenericParentTracker t = Current;
            if (t != null)
            {
                if (go == null && !id.IsEmpty() && On)
                {
                    NetworkIdentity ni = t.GetComponent<NetworkIdentity>();
                    Waiting[t] = new Pending { Holder = id, Type = type, Self = ni != null ? ni.netId.Value : 0u, Until = Time.realtimeSinceStartup + GiveUp };
                    if (_runner == null)
                    {
                        GameObject host = new GameObject("BtcTrackFix");
                        UnityEngine.Object.DontDestroyOnLoad(host);
                        _runner = host.AddComponent<BtcTrackFixRunner>();
                    }
                }
                else if (Waiting.Count > 0) Waiting.Remove(t);   // a newer update decides
            }
            return new TrackingPair(go, (TrackingTypes)type);
        }

        internal static void Tick()
        {
            if (Waiting.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            _done.Clear();
            foreach (KeyValuePair<GenericParentTracker, Pending> kv in Waiting)
            {
                GenericParentTracker t = kv.Key;
                Pending p = kv.Value;
                if (t == null) { _done.Add(t); continue; }
                NetworkIdentity ni = t.GetComponent<NetworkIdentity>();
                if (ni == null || ni.netId.Value != p.Self || CurRef(t).go != null) { _done.Add(t); continue; }   // pooled away or linked since
                GameObject go = NetworkServer.active ? NetworkServer.FindLocalObject(p.Holder) : ClientScene.FindLocalObject(p.Holder);
                if (go == null)
                {
                    if (now > p.Until) { _done.Add(t); _lost++; }
                    continue;
                }
                _done.Add(t);
                try { Changed(t, new TrackingPair(go, (TrackingTypes)p.Type)); _fixed++; }
                catch (Exception e) { BtcLog.Warn("TrackFix", t.name + " -> " + go.name + ": " + e.Message); }
            }
            for (int i = 0; i < _done.Count; i++) Waiting.Remove(_done[i]);
            if (Waiting.Count == 0 && (_fixed > 0 || _lost > 0))
            {
                BtcLog.Info("TrackFix", "late holder links applied=" + _fixed + " lost=" + _lost);
                _fixed = 0; _lost = 0;
            }
        }
    }

    internal sealed class BtcTrackFixRunner : MonoBehaviour
    {
        private void LateUpdate() { BtcTrackFix.Tick(); }
    }
}
