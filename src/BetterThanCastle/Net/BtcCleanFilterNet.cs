using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Wire for BtcCleanFilter (BC ON only). 98 request client->host {project netId, bit}; 99 full state host->clients
    /// {count, (netId, mask)*}; the state is also sent to every client that becomes ready. The host checks that the
    /// requester's faction owns the project.
    /// </summary>
    internal static class BtcCleanFilterNet
    {
        internal const short MsgRequest = 98;
        internal const short MsgState = 99;
        private static float _rateT;
        private static int _rateN;

        internal sealed class ReqMsg : MessageBase
        {
            internal uint id;
            internal int bit;
            public override void Serialize(NetworkWriter w) { w.WritePackedUInt32(id); w.WritePackedUInt32((uint)bit); }
            public override void Deserialize(NetworkReader r) { id = r.ReadPackedUInt32(); bit = (int)r.ReadPackedUInt32(); }
        }

        internal sealed class StateMsg : MessageBase
        {
            internal List<uint> ids = new List<uint>();
            internal List<int> masks = new List<int>();
            public override void Serialize(NetworkWriter w)
            {
                int n = Mathf.Min(ids.Count, 2000);
                w.WritePackedUInt32((uint)n);
                for (int i = 0; i < n; i++) { w.WritePackedUInt32(ids[i]); w.WritePackedUInt32((uint)masks[i]); }
            }
            public override void Deserialize(NetworkReader r)
            {
                int n = (int)Mathf.Min(r.ReadPackedUInt32(), 2000u);
                for (int i = 0; i < n; i++) { ids.Add(r.ReadPackedUInt32()); masks.Add((int)r.ReadPackedUInt32()); }
            }
        }

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            BtcCleanFilter.Clear();
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgRequest);
                    NetworkServer.RegisterHandler(MsgRequest, OnRequestAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("CleanFilter", "RegisterHandlers: " + e); }
        }

        private static StateMsg Encode()
        {
            StateMsg s = new StateMsg();
            foreach (KeyValuePair<long, int> kv in BtcCleanFilter.Map)
            {
                if (kv.Key <= 0 || kv.Value == BtcCleanFilter.All) continue;
                s.ids.Add((uint)kv.Key);
                s.masks.Add(kv.Value);
            }
            return s;
        }

        internal static void SendState(NetworkConnection conn)
        {
            if (conn == null || !BtcNetworkGate.Enabled || !NetworkServer.active) return;
            try { conn.Send(MsgState, Encode()); }
            catch (Exception e) { BtcLog.Error("CleanFilter", "SendState: " + e); }
        }

        internal static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StateMsg msg = Encode();
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("CleanFilter", "Broadcast: " + e); }
        }

        internal static void SendRequest(CleanupProject p, int bit)
        {
            if (!BtcNetworkGate.Enabled || NetworkServer.active) return;
            long key = BtcCleanFilter.KeyOf(p);
            if (key <= 0) return;
            UNetManager m = NetworkManager.singleton as UNetManager;
            NetworkClient c = m != null ? m.LocalNetworkClient : null;
            if (c == null || !c.isConnected) return;
            ReqMsg r = new ReqMsg();
            r.id = (uint)key;
            r.bit = bit;
            c.Send(MsgRequest, r);
        }

        private static CleanupProject Resolve(uint id)
        {
            GameObject go = NetworkServer.active ? NetworkServer.FindLocalObject(new NetworkInstanceId(id)) : ClientScene.FindLocalObject(new NetworkInstanceId(id));
            return go != null ? go.GetComponent<CleanupProject>() : null;
        }

        private static void OnRequestAtHost(NetworkMessage nm)
        {
            try
            {
                ReqMsg msg = nm.ReadMessage<ReqMsg>();
                if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
                float now = Time.unscaledTime;
                if (now - _rateT > 1f) { _rateT = now; _rateN = 0; }
                if (++_rateN > 30) return;
                if (msg.bit <= 0 || msg.bit > BtcCleanFilter.All || (msg.bit & (msg.bit - 1)) != 0) return;
                CleanupProject p = Resolve(msg.id);
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (p == null || u == null || u.faction == null || p.faction != u.faction)
                {
                    BtcLog.Warn("CleanFilter", "request rejected (project " + msg.id + ", not owned or unknown)");
                    return;
                }
                BtcCleanFilter.ApplyLocal(p, msg.bit);
                Broadcast();
                BtcLog.Info("CleanFilter", "host toggled bit " + msg.bit + " on project " + msg.id + " for conn " + nm.conn.connectionId);
            }
            catch (Exception e) { BtcLog.Error("CleanFilter", "OnRequestAtHost: " + e); }
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                StateMsg msg = nm.ReadMessage<StateMsg>();
                if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
                BtcCleanFilter.Map.Clear();
                for (int i = 0; i < msg.ids.Count; i++) BtcCleanFilter.Map[msg.ids[i]] = msg.masks[i] & BtcCleanFilter.All;
            }
            catch (Exception e) { BtcLog.Error("CleanFilter", "OnStateAtClient: " + e); }
        }
    }
}
