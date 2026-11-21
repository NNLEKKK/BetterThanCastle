using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Lifecycle.Pooling;
using Brix.Lua;
using Brix.Network;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Host option gen.editor (BtcGameplay.EditorPanelOn, synced with the gameplay rows, msg 75): every peer of a
    /// multiplayer match gets the vanilla debug/editor sidebar (Architecte.allowDebugMenu) and the red banner.
    /// Instant build already works from clients (CmdSpawnPlacedBlueprint carries the `instant` flag).
    /// Unit spawns from a client sidebar are forwarded to the host (msg 67, BC ON only): the client keeps a
    /// short-lived, inactive local ghost so the sidebar Lua can call EquipTool on it; tools are recorded and sent along.
    /// BC OFF / option off / singleplayer: nothing changes (no patch body runs, no message).
    /// </summary>
    internal static class BtcEditorShare
    {
        internal const short MsgSpawn = 67;
        private const int MaxTools = 8;

        private static bool _was;
        private static bool _forced;

        internal static bool Active
        {
            get
            {
                return BtcNetworkGate.Enabled && Neo.IsMultiplayer && BtcNetManager.InSession && BtcGameplay.EditorPanelOn;
            }
        }

        /// <summary>Once per frame (RPM OnGUI repaint).</summary>
        internal static void Tick()
        {
            bool on = Active;
            if (on != _was)
            {
                _was = on;
                BtcGameplay.EditorWarn = on;
                if (!on && _forced)
                {
                    _forced = false;
                    SetAllow(false);
                }
                BtcLog.Info("Editor", on ? "editor panel shared by host: ON" : "editor panel share: OFF");
            }
            // Architecte.Start resets allowDebugMenu at every level load: re-apply in the match scene.
            if (on && !BtcGameplay.InLobby && !Brix.Engine.Architecte._allowDebugMenu)
            {
                _forced = true;
                SetAllow(true);
            }
            if (_pending.Count > 0) FlushPending();
        }

        // Field + Lua hook directly: the property setter would also mark the map as a debug map (saved in the meta).
        private static void SetAllow(bool v)
        {
            try
            {
                if (Brix.Engine.Architecte._allowDebugMenu == v) return;
                Brix.Engine.Architecte._allowDebugMenu = v;
                LuaHookBase.InvokeFromCLR(UIGameObserver.lua_OnSetAllowDebugMode);
                BtcLog.Info("Editor", "allowDebugMenu=" + v);
            }
            catch (Exception e) { BtcLog.Error("Editor", "SetAllow: " + e); }
        }

        // ------------------------------------------------------------------ client: forward sidebar spawns
        private sealed class Pending
        {
            internal Factory.AssetKey key;
            internal uint faction;
            internal Vector3 pos;
            internal Quaternion rot;
            internal GameObject ghost;
            internal readonly List<Factory.AssetKey> tools = new List<Factory.AssetKey>();
        }

        private static readonly Dictionary<int, Pending> _pending = new Dictionary<int, Pending>();
        private static readonly List<int> _flushIds = new List<int>();

        /// <summary>LuaFactory.InstantiateFromCLR Prefix (client). True = handled (ghost returned in result).</summary>
        internal static bool TryForward(Factory.AssetKey key, Faction faction, Vector3 position, Quaternion rotation, out LuaDriver result)
        {
            result = null;
            if (NetworkServer.active || !Active) return false;
            GameObject prefab = Factory.Peek(key);
            // only networked objects: vanilla cannot spawn those on a client at all (NetworkServer.Spawn fails)
            if (prefab == null || prefab.GetComponent<NetworkIdentity>() == null) return false;
            GameObject go;
            ObjectPoolSingleton.Request(out go, key, position, rotation);
            if (go == null) return false;
            go.SetActive(false);
            Pending p = new Pending { key = key, faction = faction != null ? faction.netId.Value : 0u, pos = position, rot = rotation, ghost = go };
            _pending[go.GetInstanceID()] = p;
            result = go.GetComponent<LuaDriver>();
            return true;
        }

        /// <summary>Toolbag.lua_EquipTool Prefix (client): record tools equipped on a ghost. True = handled.</summary>
        internal static bool TryRecordTool(CallbackArguments args)
        {
            if (_pending.Count == 0 || args.Count < 2) return false;
            Component c = BtcSpawnPick.Obj(args, 0) as Component;
            Pending p;
            if (c == null || !_pending.TryGetValue(c.gameObject.GetInstanceID(), out p)) return false;
            LuaStaticDriver t = BtcSpawnPick.Obj(args, 1) as LuaStaticDriver;
            if (t != null && p.tools.Count < MaxTools) p.tools.Add(t.AssetKey);
            return true;
        }

        private static void FlushPending()
        {
            _flushIds.Clear();
            foreach (KeyValuePair<int, Pending> kv in _pending) _flushIds.Add(kv.Key);
            for (int i = 0; i < _flushIds.Count; i++)
            {
                Pending p = _pending[_flushIds[i]];
                _pending.Remove(_flushIds[i]);
                try
                {
                    UNetManager m = UNetManager.instance;
                    NetworkClient c = m != null ? m.LocalNetworkClient : null;
                    if (c != null && c.isConnected && BtcNetworkGate.Enabled)
                    {
                        SpawnMsg msg = new SpawnMsg { key = p.key, faction = p.faction, pos = p.pos, rot = p.rot, tools = p.tools };
                        c.Send(MsgSpawn, msg);
                        BtcLog.Info("Editor", "spawn request sent: " + p.key.Factory + "/" + p.key.Name + " tools " + p.tools.Count);
                    }
                }
                catch (Exception e) { BtcLog.Error("Editor", "flush: " + e); }
                try { if (p.ghost != null) ObjectPoolSingleton.Release(p.ghost); }
                catch (Exception e) { BtcLog.Error("Editor", "ghost release: " + e); }
            }
        }

        // ------------------------------------------------------------------ host
        private static float _rateT;
        private static int _rateN;

        internal static void RegisterHandlers()
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                _pending.Clear();
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgSpawn);
                    NetworkServer.RegisterHandler(MsgSpawn, OnSpawnAtHost);
                }
            }
            catch (Exception e) { BtcLog.Error("Editor", "RegisterHandlers: " + e); }
        }

        private static void OnSpawnAtHost(NetworkMessage nm)
        {
            try
            {
                SpawnMsg msg = nm.ReadMessage<SpawnMsg>();
                if (!NetworkServer.active || !BtcNetworkGate.Enabled || !BtcGameplay.EditorPanelOn || BtcGameplay.InLobby)
                {
                    BtcLog.Warn("Editor", "spawn request ignored (editor panel off or not in a match)");
                    return;
                }
                float now = Time.unscaledTime;
                if (now - _rateT > 1f) { _rateT = now; _rateN = 0; }
                if (++_rateN > 40) return;
                if (Factory.Peek(msg.key) == null)
                {
                    BtcLog.Warn("Editor", "spawn request: unknown asset " + msg.key.Factory + "/" + msg.key.Name);
                    return;
                }
                Faction f = null;
                GameObject fo = msg.faction != 0u ? NetworkServer.FindLocalObject(new NetworkInstanceId(msg.faction)) : null;
                if (fo != null) f = fo.GetComponent<Faction>();
                // player factions: the sidebar picks "the last owned faction"; use the requesting player's own faction
                if (f == null || f.HasOwner())
                {
                    User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                    if (u != null && u.faction != null) f = u.faction;
                }
                LuaDriver d = LuaFactory.InstantiateFromCLR(msg.key, f, msg.pos, msg.rot);
                Toolbag tb = d != null ? d.GetComponent<Toolbag>() : null;
                if (tb != null && msg.tools != null)
                {
                    for (int i = 0; i < msg.tools.Count && i < MaxTools; i++)
                    {
                        if (Factory.Peek(msg.tools[i]) != null) tb.EquipTool(msg.tools[i]);
                    }
                }
                BtcLog.Info("Editor", "host spawned " + msg.key.Name + " for conn " + nm.conn.connectionId + " tools " + (msg.tools != null ? msg.tools.Count : 0));
            }
            catch (Exception e) { BtcLog.Error("Editor", "OnSpawnAtHost: " + e); }
        }

        internal sealed class SpawnMsg : MessageBase
        {
            internal Factory.AssetKey key;
            internal uint faction;
            internal Vector3 pos;
            internal Quaternion rot;
            internal List<Factory.AssetKey> tools;

            public override void Serialize(NetworkWriter w)
            {
                w.Write(key.Factory ?? string.Empty);
                w.Write(key.Name ?? string.Empty);
                w.Write(faction);
                w.Write(pos);
                w.Write(rot);
                int n = tools != null ? Mathf.Min(tools.Count, MaxTools) : 0;
                w.Write((byte)n);
                for (int i = 0; i < n; i++)
                {
                    w.Write(tools[i].Factory ?? string.Empty);
                    w.Write(tools[i].Name ?? string.Empty);
                }
            }

            public override void Deserialize(NetworkReader r)
            {
                string fa = r.ReadString();
                string na = r.ReadString();
                key = new Factory.AssetKey(fa, na);
                faction = r.ReadUInt32();
                pos = r.ReadVector3();
                rot = r.ReadQuaternion();
                int n = Mathf.Min(r.ReadByte(), MaxTools);
                tools = new List<Factory.AssetKey>(n);
                for (int i = 0; i < n; i++)
                {
                    string tf = r.ReadString();
                    string tn = r.ReadString();
                    tools.Add(new Factory.AssetKey(tf, tn));
                }
            }
        }
    }
}
