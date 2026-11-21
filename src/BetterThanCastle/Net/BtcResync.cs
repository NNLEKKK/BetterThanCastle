using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// BCv2 quick return in place (client stays in its loaded world after an in-place reconnect).
    /// 1) The scene is bound to the NEW connection as soon as the hello goes out (BindTo), before any host message: the host's
    ///    owner message then attaches our player to the new connection and orders work (they were sent on the dead one).
    /// 2) While we were away we missed the host's destroy messages: objects it no longer has (picked-up stones, dead units)
    ///    still stood here ("floating pebbles"). The host sends the ids of every object it has (msg 115, same ordered channel
    ///    as spawns, in chunks); the client removes what is not on that list.
    /// </summary>
    internal static class BtcResync
    {
        internal const short MsgAlive = 115;
        private const int ChunkIds = 300;
        private static readonly HashSet<uint> _alive = new HashSet<uint>();
        private static bool _collecting;

        internal sealed class AliveMsg : MessageBase
        {
            public bool last;
            public uint[] ids = new uint[0];
            public override void Serialize(NetworkWriter w)
            {
                w.Write(last);
                w.WritePackedUInt32((uint)ids.Length);
                for (int i = 0; i < ids.Length; i++) w.WritePackedUInt32(ids[i]);
            }
            public override void Deserialize(NetworkReader r)
            {
                last = r.ReadBoolean();
                uint n = r.ReadPackedUInt32();
                ids = new uint[n];
                for (int i = 0; i < n; i++) ids[i] = r.ReadPackedUInt32();
            }
        }

        // ------------------------------------------------------------------ host
        /// <summary>Host, right after the quick return re-attached a player: every object id the host has, in ordered chunks.</summary>
        internal static void SendAlive(NetworkConnection conn)
        {
            if (conn == null || !BtcNetworkGate.Enabled) return;
            List<uint> ids = new List<uint>(NetworkServer.objects.Count);
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in NetworkServer.objects) ids.Add(kv.Key.Value);
            int sent = 0;
            do
            {
                int n = Math.Min(ChunkIds, ids.Count - sent);
                AliveMsg m = new AliveMsg { ids = ids.GetRange(sent, n).ToArray() };
                sent += n;
                m.last = sent >= ids.Count;
                conn.Send(MsgAlive, m);
            }
            while (sent < ids.Count);
            BtcLog.Info("Net", "resync: sent " + ids.Count + " live object ids to conn " + conn.connectionId);
        }

        // ------------------------------------------------------------------ client
        internal static void RegisterClient(NetworkClient c)
        {
            if (c == null) return;
            if (c.handlers.ContainsKey(MsgAlive)) c.UnregisterHandler(MsgAlive);
            c.RegisterHandler(MsgAlive, OnAlive);
        }

        private static void OnAlive(NetworkMessage nm)
        {
            try
            {
                AliveMsg m = nm.ReadMessage<AliveMsg>();
                if (!_collecting) { _alive.Clear(); _collecting = true; }
                for (int i = 0; i < m.ids.Length; i++) _alive.Add(m.ids[i]);
                if (!m.last) return;
                _collecting = false;
                Sweep();
            }
            catch (Exception e) { BtcLog.Error("Net", "resync alive: " + e); }
        }

        private static void Sweep()
        {
            int removed = 0;
            Dictionary<NetworkInstanceId, NetworkIdentity> objs = ClientScene.objects;
            List<NetworkInstanceId> gone = new List<NetworkInstanceId>();
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in objs) if (!_alive.Contains(kv.Key.Value)) gone.Add(kv.Key);
            FieldInfo nsf = typeof(ClientScene).GetField("s_NetworkScene", BindingFlags.Static | BindingFlags.NonPublic);
            object ns = nsf != null ? nsf.GetValue(null) : null;
            MethodInfo remove = ns != null ? ns.GetType().GetMethod("RemoveLocalObject", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) : null;
            MethodInfo onDestroy = typeof(NetworkIdentity).GetMethod("OnNetworkDestroy", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < gone.Count; i++)
            {
                NetworkIdentity uv;
                if (!objs.TryGetValue(gone[i], out uv)) continue;
                try
                {
                    if (uv != null && uv.isLocalPlayer) continue;
                    if (uv != null && onDestroy != null) onDestroy.Invoke(uv, null);
                    if (remove != null) remove.Invoke(ns, new object[] { gone[i] }); else objs.Remove(gone[i]);
                    if (uv != null) UnityEngine.Object.Destroy(uv.gameObject);
                    removed++;
                }
                catch (Exception e) { BtcLog.Warn("Net", "resync: remove " + gone[i] + ": " + e.Message); }
            }
            BtcLog.Info("Net", "resync: host has " + _alive.Count + " objects, here " + objs.Count + " left, removed " + removed + " the host no longer has");
            _alive.Clear();
        }

        /// <summary>Client, in-place reconnect hello sent: make the new connection the ready one, keep our local player on it.</summary>
        internal static void BindTo(NetworkConnection c)
        {
            if (c == null) return;
            Type t = typeof(ClientScene);
            BindingFlags bf = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo rc = t.GetField("s_ReadyConnection", bf), ir = t.GetField("s_IsReady", bf), lp = t.GetField("s_LocalPlayers", bf);
            if (rc == null || ir == null) { BtcLog.Warn("Net", "resync: ClientScene ready fields not found"); return; }
            rc.SetValue(null, c);
            ir.SetValue(null, true);
            int moved = 0;
            List<PlayerController> players = lp != null ? lp.GetValue(null) as List<PlayerController> : null;
            MethodInfo setPc = typeof(NetworkConnection).GetMethod("SetPlayerController", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo setConn = typeof(NetworkIdentity).GetMethod("SetConnectionToServer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerController pc = players[i];
                    if (pc == null || pc.unetView == null) continue;
                    try
                    {
                        if (setPc != null) setPc.Invoke(c, new object[] { pc });
                        if (setConn != null) setConn.Invoke(pc.unetView, new object[] { c });
                        moved++;
                    }
                    catch (Exception e) { BtcLog.Warn("Net", "resync: move player: " + e.Message); }
                }
            BtcLog.Info("Net", "resync: scene bound to the new connection " + c.connectionId + " (" + moved + " local player(s) moved)");
        }
    }
}
