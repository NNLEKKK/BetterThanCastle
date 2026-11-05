using System;
using System.Collections.Generic;
using Brix.Game;
using Brix.Game.Semantique;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Wire for BtcRackFilter (BC ON only; nothing is registered or sent with BC OFF).
    /// 102 request client->host {rack netId, kit+1}; 103 full state host->clients {count, (netId, kit)*}. The host checks that the
    /// requester's faction owns the rack. State is also sent to every client that becomes ready (BtcGameplay.OnClientReady).
    /// </summary>
    internal static class BtcRackFilterNet
    {
        internal const short MsgRequest = 102;
        internal const short MsgState = 103;
        private static float _rateT;
        private static int _rateN;

        internal sealed class ReqMsg : MessageBase
        {
            internal uint id;
            internal int kit;
            public override void Serialize(NetworkWriter w) { w.WritePackedUInt32(id); w.Write((byte)(kit + 1)); }
            public override void Deserialize(NetworkReader r) { id = r.ReadPackedUInt32(); kit = r.ReadByte() - 1; }
        }

        internal sealed class StateMsg : MessageBase
        {
            internal List<uint> ids = new List<uint>();
            internal List<int> kits = new List<int>();
            public override void Serialize(NetworkWriter w)
            {
                int n = Mathf.Min(ids.Count, 4000);
                w.WritePackedUInt32((uint)n);
                for (int i = 0; i < n; i++) { w.WritePackedUInt32(ids[i]); w.Write((byte)kits[i]); }
            }
            public override void Deserialize(NetworkReader r)
            {
                int n = (int)Mathf.Min(r.ReadPackedUInt32(), 4000u);
                for (int i = 0; i < n; i++) { ids.Add(r.ReadPackedUInt32()); kits.Add(r.ReadByte()); }
            }
        }

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            BtcRackFilter.Clear();
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
            catch (Exception e) { BtcLog.Error("RackFilter", "RegisterHandlers: " + e); }
        }

        private static StateMsg Encode()
        {
            StateMsg s = new StateMsg();
            foreach (KeyValuePair<long, int> kv in BtcRackFilter.Map)
            {
                if (kv.Key <= 0 || kv.Value < 0) continue;
                s.ids.Add((uint)kv.Key);
                s.kits.Add(kv.Value);
            }
            return s;
        }

        internal static void SendState(NetworkConnection conn)
        {
            if (conn == null || !BtcNetworkGate.Enabled || !NetworkServer.active) return;
            try { conn.Send(MsgState, Encode()); }
            catch (Exception e) { BtcLog.Error("RackFilter", "SendState: " + e); }
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
            catch (Exception e) { BtcLog.Error("RackFilter", "Broadcast: " + e); }
        }

        internal static void SendRequest(ToolRack rack, int kit)
        {
            if (!BtcNetworkGate.Enabled || NetworkServer.active) return;
            long key = BtcRackFilter.KeyOf(rack);
            if (key <= 0) return;
            UNetManager m = NetworkManager.singleton as UNetManager;
            NetworkClient c = m != null ? m.LocalNetworkClient : null;
            if (c == null || !c.isConnected) return;
            ReqMsg r = new ReqMsg();
            r.id = (uint)key;
            r.kit = kit;
            c.Send(MsgRequest, r);
        }

        private static ToolRack Resolve(uint id)
        {
            GameObject go = NetworkServer.active ? NetworkServer.FindLocalObject(new NetworkInstanceId(id)) : ClientScene.FindLocalObject(new NetworkInstanceId(id));
            return go != null ? go.GetComponent<ToolRack>() : null;
        }

        private static void OnRequestAtHost(NetworkMessage nm)
        {
            try
            {
                ReqMsg msg = nm.ReadMessage<ReqMsg>();
                if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
                float now = Time.unscaledTime;
                if (now - _rateT > 1f) { _rateT = now; _rateN = 0; }
                if (++_rateN > 20) return;
                if (msg.kit < -1 || msg.kit >= BtcRackFilter.KitNames.Length) return;
                ToolRack rack = Resolve(msg.id);
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (rack == null || u == null || u.faction == null || rack.faction != u.faction)
                {
                    BtcLog.Warn("RackFilter", "request rejected (rack " + msg.id + ", not owned or unknown)");
                    return;
                }
                BtcRackFilter.ApplyLocal(rack, msg.kit);
                Broadcast();
                BtcLog.Info("RackFilter", "host applied " + (msg.kit < 0 ? "Any" : BtcRackFilter.KitNames[msg.kit]) + " on rack " + msg.id + " for conn " + nm.conn.connectionId);
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "OnRequestAtHost: " + e); }
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                StateMsg msg = nm.ReadMessage<StateMsg>();
                if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
                HashSet<long> touched = new HashSet<long>(BtcRackFilter.Map.Keys);
                BtcRackFilter.Map.Clear();
                for (int i = 0; i < msg.ids.Count; i++)
                {
                    if (msg.kits[i] >= BtcRackFilter.KitNames.Length) continue;
                    BtcRackFilter.Map[msg.ids[i]] = msg.kits[i];
                    touched.Add(msg.ids[i]);
                }
                foreach (long k in touched)
                {
                    if (k <= 0) continue;
                    ToolRack rack = Resolve((uint)k);
                    if (rack != null) BtcRackFilter.Refresh(rack);
                }
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "OnStateAtClient: " + e); }
        }
    }
}
