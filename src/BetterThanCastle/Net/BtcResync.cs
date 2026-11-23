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
    ///    as spawns, in chunks); the client removes what is not on that list, only once it holds all `total` ids (a lost or
    ///    reordered chunk must never sweep live objects), the same way the game's own destroy message does (pool release).
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
            // ids in the whole stream; appended after the ids so an older client still reads the message. -1 = older host (no total)
            public int total = -1;
            public override void Serialize(NetworkWriter w)
            {
                w.Write(last);
                w.WritePackedUInt32((uint)ids.Length);
                for (int i = 0; i < ids.Length; i++) w.WritePackedUInt32(ids[i]);
                w.WritePackedUInt32((uint)total);
            }
            public override void Deserialize(NetworkReader r)
            {
                last = r.ReadBoolean();
                uint n = r.ReadPackedUInt32();
                ids = new uint[n];
                for (int i = 0; i < n; i++) ids[i] = r.ReadPackedUInt32();
                total = r.Position < r.Length ? (int)r.ReadPackedUInt32() : -1;
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
                AliveMsg m = new AliveMsg { ids = ids.GetRange(sent, n).ToArray(), total = ids.Count };
                sent += n;
                m.last = sent >= ids.Count;
                if (!conn.Send(MsgAlive, m))
                {
                    // the client never reaches `total` and keeps its objects: stale ones are better than wrongly removed live ones
                    BtcLog.Warn("Net", "resync: send failed after " + (sent - n) + "/" + ids.Count + " ids to conn " + conn.connectionId + ", no sweep");
                    return;
                }
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
                if (m.total >= 0 ? _alive.Count < m.total : !m.last) return;
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
            // same steps as ClientScene.OnObjectDestroy: the game pools its networked objects (FactoryRegistrator unspawn
            // handler -> ObjectPoolSingleton.Release), so a plain Destroy would shrink the pool and break later spawns
            Type nsType = typeof(ClientScene).Assembly.GetType("UnityEngine.Networking.NetworkScene");
            MethodInfo unspawn = nsType == null ? null : nsType.GetMethod("InvokeUnSpawnHandler", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo markReset = typeof(NetworkIdentity).GetMethod("MarkForReset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < gone.Count; i++)
            {
                NetworkIdentity uv;
                if (!objs.TryGetValue(gone[i], out uv)) continue;
                try
                {
                    if (uv != null && uv.isLocalPlayer) continue;
                    if (uv != null && onDestroy != null) onDestroy.Invoke(uv, null);
                    if (uv != null && (unspawn == null || !(bool)unspawn.Invoke(null, new object[] { uv.assetId, uv.gameObject })))
                    {
                        if (uv.sceneId.IsEmpty()) UnityEngine.Object.Destroy(uv.gameObject);
                        else { uv.gameObject.SetActive(false); ClientScene.spawnableObjects[uv.sceneId] = uv; }
                    }
                    if (remove != null) remove.Invoke(ns, new object[] { gone[i] }); else objs.Remove(gone[i]);
                    if (uv != null && markReset != null) markReset.Invoke(uv, null);
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
            // a new resync stream starts here: drop ids left by a stream the previous connection cut short
            _collecting = false;
            _alive.Clear();
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
