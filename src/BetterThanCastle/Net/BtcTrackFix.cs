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
        private const float ParkLimit = 1800f;
        [ThreadStatic] internal static GenericParentTracker Current;
        private static readonly Dictionary<GenericParentTracker, Pending> Waiting = new Dictionary<GenericParentTracker, Pending>();
        private static readonly HashSet<GenericParentTracker> Parked = new HashSet<GenericParentTracker>();
        private static readonly List<GenericParentTracker> _done = new List<GenericParentTracker>();
        private static readonly List<GenericParentTracker> _kids = new List<GenericParentTracker>();
        private static readonly AccessTools.FieldRef<GenericParentTracker, TrackingPair> CurRef = AccessTools.FieldRefAccess<GenericParentTracker, TrackingPair>("currentTracker");
        private static readonly AccessTools.FieldRef<GenericParentTracker, TrackingPair> PrevRef = AccessTools.FieldRefAccess<GenericParentTracker, TrackingPair>("previousTracker");
        private static int _parked;
        private static readonly Action<GenericParentTracker, TrackingPair> Changed =
            AccessTools.MethodDelegate<Action<GenericParentTracker, TrackingPair>>(AccessTools.Method(typeof(GenericParentTracker), "CurrentTrackerChanged"));
        private static int _fixed, _lost;
        private static BtcTrackFixRunner _runner;

        internal static bool On { get { return BtcNetworkGate.Enabled || BtcReplay.Active; } }

        /// <summary>The synced link type. ToolParentTracker.TrackingType() is a local field (grip after a toolbag, kept by the pool) that
        /// clients never get updated, so a tool lying in a rack can still answer "grip" there.</summary>
        internal static TrackingTypes LinkType(GenericParentTracker t) { return (TrackingTypes)CurRef(t).type; }

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
                    EnsureRunner();
                }
                else
                {
                    if (Waiting.Count > 0) Waiting.Remove(t);   // a newer update decides
                    if (Parked.Count > 0 && Parked.Remove(t)) t.gameObject.SetActive(true);
                }
            }
            return new TrackingPair(go, (TrackingTypes)type);
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            GameObject host = new GameObject("BtcTrackFix");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<BtcTrackFixRunner>();
        }

        /// <summary>
        /// ObjectPoolSingleton.ResetGameObject prefix, pure client. A holder the client drops (fog hide, replay kill) goes back to its pool
        /// with its tracked children: kit tools stay under the pooled unit's limbs, Recepteur.Wipe releases stored items locally although
        /// UNET still lists them alive. When the server shows the holder again it never resends those links (they did not change), so the
        /// unit comes back without its kit, a rack/stockpile empty, and the pooled instance may later show a stale tool on another unit.
        /// Here each still-registered child is taken out first (holder sets cleaned, hidden, link cleared) and parked until its holder's
        /// netId exists again; its own destroy or a newer tracker update ends the parking.
        /// </summary>
        internal static void BeforeRelease(GameObject go)
        {
            if (go == null || NetworkServer.active || !On || ClientScene.objects == null) return;
            NetworkIdentity root = go.GetComponent<NetworkIdentity>();
            if (root == null || root.netId.IsEmpty()) return;
            _kids.Clear();
            go.GetComponentsInChildren<GenericParentTracker>(true, _kids);
            for (int i = 0; i < _kids.Count; i++)
            {
                GenericParentTracker t = _kids[i];
                if (t == null || t.gameObject == go) continue;
                try { Park(t, root); }
                catch (Exception e) { BtcLog.Warn("TrackFix", "park " + t.name + ": " + e.Message); }
            }
            _kids.Clear();
        }

        private static void Park(GenericParentTracker t, NetworkIdentity root)
        {
            TrackingPair cur = CurRef(t);
            GameObject holder = cur.go;
            if (holder == null) return;
            NetworkIdentity hni = holder.GetComponentInParent<NetworkIdentity>();
            if (hni != root) return;   // nested item: its own holder is not the one leaving
            NetworkIdentity tni = t.GetComponent<NetworkIdentity>(), reg;
            if (tni == null || tni.netId.IsEmpty() || !ClientScene.objects.TryGetValue(tni.netId, out reg) || reg != tni) return;
            GameObject item = t.gameObject;
            if (Brix.Game.Components.GameComponentExtender.IsNullOrReleased(item)) return;
            Brix.Game.Semantique.Recepteur r = holder.GetComponent<Brix.Game.Semantique.Recepteur>();
            Brix.Game.Semantique.IDescriptor d = item.GetComponent<Brix.Game.Semantique.IDescriptor>();
            if (r != null && d != null && r.StoredItems.Contains(d)) Brix.Game.Semantique.Recepteur.RemoveFrom(r, d);
            Brix.Game.AI.Toolbag tb = holder.GetComponent<Brix.Game.AI.Toolbag>();
            if (tb != null)
            {
                Brix.Game.AI.Toolbag.ToolLimbPair p = tb.GetToolLimbPair(Grip.SlotOf(item));
                if (p != null && p.Tool == item) p.Tool = null;
            }
            TrackingPair none = new TrackingPair(null, (TrackingTypes)cur.type);
            CurRef(t) = none;
            PrevRef(t) = none;
            Brix.Utils.ClasseMoi.Move(item);
            item.SetActive(false);
            Waiting[t] = new Pending { Holder = hni.netId, Type = cur.type, Self = tni.netId.Value, Until = Time.realtimeSinceStartup + ParkLimit };
            Parked.Add(t);
            _parked++;
            EnsureRunner();
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
                if (ni == null || ni.netId.Value != p.Self || CurRef(t).go != null || Brix.Game.Components.GameComponentExtender.IsNullOrReleased(t.gameObject))
                {
                    _done.Add(t);   // pooled away or linked since
                    continue;
                }
                GameObject go = NetworkServer.active ? NetworkServer.FindLocalObject(p.Holder) : ClientScene.FindLocalObject(p.Holder);
                if (go == null || Brix.Game.Components.GameComponentExtender.IsNullOrReleased(go))
                {
                    if (now > p.Until) { _done.Add(t); _lost++; }
                    continue;
                }
                _done.Add(t);
                if (Parked.Remove(t)) t.gameObject.SetActive(true);   // the holder's tracker decides the final state (racks hide stored items)
                try { Changed(t, new TrackingPair(go, (TrackingTypes)p.Type)); _fixed++; }
                catch (Exception e) { BtcLog.Warn("TrackFix", t.name + " -> " + go.name + ": " + e.Message); }
            }
            for (int i = 0; i < _done.Count; i++) { Waiting.Remove(_done[i]); Parked.Remove(_done[i]); }
            if (Waiting.Count == 0 && (_fixed > 0 || _lost > 0 || _parked > 0))
            {
                BtcLog.Info("TrackFix", "late holder links applied=" + _fixed + " lost=" + _lost + " parked=" + _parked);
                _fixed = 0; _lost = 0; _parked = 0;
            }
        }
    }

    internal sealed class BtcTrackFixRunner : MonoBehaviour
    {
        private void LateUpdate() { BtcTrackFix.Tick(); }
    }
}
