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
    /// The C# editor sidebar (BtcGhDev) sends every world-changing button to the host instead (msg 137: whitelisted sidebar
    /// statement run with the client's faction, selection and camera point; explosion picker clicks), and the host tells
    /// everyone who used which editor function (msg 138, HUD notice).
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
            BtcGhDev.Poll();
            BtcMovieTime.Tick();
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
                try { BtcGhRightBar.CheckDev(); BtcGameHud.RefreshAll(); } catch { }
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

        // ------------------------------------------------------------------ editor actions (C# editor sidebar, BtcGhDev): client -> host 137, notice host -> all 138
        internal const short MsgAct = 137, MsgNote = 138;
        internal const byte KindRun = 0, KindExplosion = 1, KindNote = 2, KindMovie = 3;
        /// <summary>Notes starting with this come from the movie sidebar (BtcMovieTime): shown as "movie panel: ...".</summary>
        internal const string MovieMark = "\u0002";

        /// <summary>This peer is a client of a match whose host shares the editor panel: world changes go to the host.</summary>
        internal static bool Remote { get { return !NetworkServer.active && Active; } }

        /// <summary>Client: one editor action for the host (run a whitelisted sidebar statement, an explosion, or just tell the others).</summary>
        internal static void SendAct(byte kind, string lua, string note, object faction, object selected, Vector3? pos = null, float force = 0f)
        {
            try
            {
                UNetManager m = UNetManager.instance;
                NetworkClient c = m != null ? m.LocalNetworkClient : null;
                if (c == null || !c.isConnected || !BtcNetworkGate.Enabled) return;
                ActMsg msg = new ActMsg { kind = kind, lua = lua ?? "", note = note ?? "", force = force };
                Faction f = faction as Faction;
                if (f == null && faction is Component) f = ((Component)faction).GetComponent<Faction>();
                if (f != null)
                {
                    // the faction netId alone was not enough (clients' picks fell back to their own faction): send its owner and AI flag too
                    msg.faction = f.netId.Value;
                    User ow = null;
                    try { ow = f.Owner; } catch (Exception) { }
                    if (ow != null) msg.owner = ow.netId.Value;
                    msg.ai = f.isAI;
                }
                if (kind == KindRun) BtcLog.Info("Editor", "action sent: " + note + " faction " + (f != null ? f.name + " nid " + msg.faction + " owner " + msg.owner + " ai " + msg.ai : "none (" + (faction != null ? faction.GetType().Name : "null") + ")"));
                Component sc = selected as Component;
                NetworkIdentity ni = sc != null ? sc.GetComponentInParent<NetworkIdentity>() : null;
                if (ni != null) msg.sel = ni.netId.Value;
                msg.hasPos = true;
                msg.pos = pos.HasValue ? pos.Value : Brix.Input.CameraScript.Target;
                c.Send(MsgAct, msg);
            }
            catch (Exception e) { BtcLog.Error("Editor", "SendAct: " + e); }
        }

        private static void OnActAtHost(NetworkMessage nm)
        {
            try
            {
                ActMsg msg = nm.ReadMessage<ActMsg>();
                if (!NetworkServer.active || !Active || BtcGameplay.InLobby) { BtcLog.Warn("Editor", "action ignored (editor panel off or not in a match)"); return; }
                float now = Time.unscaledTime;
                if (now - _rateT > 1f) { _rateT = now; _rateN = 0; }
                if (++_rateN > 40) return;
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                string who = UserName(u);
                if (msg.kind == KindMovie)
                {
                    if (!BtcMovieTime.HostApply(msg.lua, nm.conn.connectionId, u)) return;
                    msg.note = MovieMark + msg.note;
                }
                else if (msg.kind == KindExplosion)
                {
                    if (!msg.hasPos) return;
                    Brix.Engine.Architecte.ExplosionSimple(Faction.DefaultAllied, msg.pos, Mathf.Clamp(msg.force, 0f, 50f), "Explosion");
                }
                else if (msg.kind == KindRun)
                {
                    if (!BtcGhDev.IsHostCmd(msg.lua)) { BtcLog.Warn("Editor", "action refused (not an editor button): " + msg.lua); return; }
                    Faction f = FindFaction(msg);
                    BtcLog.Info("Editor", "action faction: nid " + msg.faction + " owner " + msg.owner + " ai " + msg.ai + " -> " + (f != null ? f.name : "requester's own"));
                    if (f == null && u != null) f = u.faction;
                    LuaDriver sel = null;
                    GameObject so = msg.sel != 0u ? NetworkServer.FindLocalObject(new NetworkInstanceId(msg.sel)) : null;
                    if (so != null) sel = so.GetComponentInChildren<LuaDriver>();
                    if (!BtcGhDev.RunFor(msg.lua, f, sel, msg.hasPos ? (Vector3?)msg.pos : null)) return;
                }
                BtcLog.Info("Editor", "action from conn " + nm.conn.connectionId + " (" + who + "): " + msg.note);
                Announce(u, msg.note);
            }
            catch (Exception e) { BtcLog.Error("Editor", "OnActAtHost: " + e); }
        }

        private static Faction FindFaction(ActMsg msg)
        {
            GameObject fo = msg.faction != 0u ? NetworkServer.FindLocalObject(new NetworkInstanceId(msg.faction)) : null;
            Faction f = fo != null ? fo.GetComponent<Faction>() : null;
            if (f == null && msg.faction != 0u)
            {
                List<Faction> all = Faction.GetAllFactions();
                for (int i = 0; all != null && i < all.Count; i++) if (all[i] != null && all[i].netId.Value == msg.faction) { f = all[i]; break; }
            }
            if (f == null && msg.owner != 0u)
            {
                GameObject uo = NetworkServer.FindLocalObject(new NetworkInstanceId(msg.owner));
                User ou = uo != null ? uo.GetComponent<User>() : null;
                if (ou != null) f = ou.faction;
            }
            if (f == null && msg.ai) f = Faction.DefaultEnemy;
            return f;
        }

        /// <summary>The host itself used the shared editor panel.</summary>
        internal static void AnnounceLocal(string note)
        {
            if (!NetworkServer.active || !Active || string.IsNullOrEmpty(note)) return;
            Announce(User.LocalUser, note);
        }

        private static readonly Dictionary<string, float> _noteLast = new Dictionary<string, float>();

        // everyone but the player who used the function gets the notice
        private static void Announce(User actor, string note)
        {
            if (string.IsNullOrEmpty(note)) return;
            string who = UserName(actor);
            uint actorId = actor != null ? actor.netId.Value : 0u;
            // one line per player and action every 2 s (explosion clicks, spawn spam)
            string k = who + "|" + note;
            float now = Time.unscaledTime, last;
            if (_noteLast.TryGetValue(k, out last) && now - last < 2f) return;
            _noteLast[k] = now;
            if (_noteLast.Count > 64) _noteLast.Clear();
            if (actor != User.LocalUser) ShowNote(who, note);
            try { NetworkServer.SendToAll(MsgNote, new NoteMsg { who = who, note = note, actor = actorId }); }
            catch (Exception e) { BtcLog.Error("Editor", "note send: " + e); }
        }

        private static void OnNoteAtClient(NetworkMessage nm)
        {
            try
            {
                NoteMsg m = nm.ReadMessage<NoteMsg>();
                User me = User.LocalUser;
                if (m.actor != 0u && me != null && me.netId.Value == m.actor) return;
                ShowNote(m.who, m.note);
            }
            catch (Exception e) { BtcLog.Error("Editor", "OnNoteAtClient: " + e); }
        }

        private static void ShowNote(string who, string note)
        {
            BtcLog.Info("Editor", "notice: " + who + " - " + note);
            bool movie = note.StartsWith(MovieMark, StringComparison.Ordinal);
            if (movie) note = note.Substring(MovieMark.Length);
            string panel = movie ? BtcReplayUI.T("movie panel: ", "муви панель: ") : BtcReplayUI.T("editor panel: ", "панель редактора: ");
            BtcHud.Notice(who + " - " + panel + note, BtcReplayIcons.Check, BtcHud.Warn);
        }

        private static string UserName(User u)
        {
            if (u == null) return "?";
            string n = null;
            try { n = u.ScreenName; } catch (Exception) { }
            if (string.IsNullOrEmpty(n)) n = u.name;
            if (n != null && n.Length > 40) n = n.Substring(0, 40);
            return n;
        }

        internal sealed class ActMsg : MessageBase
        {
            internal byte kind;
            internal string lua, note;
            internal uint faction, sel, owner;
            internal bool hasPos, ai;
            internal Vector3 pos;
            internal float force;

            public override void Serialize(NetworkWriter w)
            {
                w.Write(kind); w.Write(lua ?? ""); w.Write(note ?? ""); w.Write(faction); w.Write(sel);
                w.Write(hasPos); w.Write(pos); w.Write(force); w.Write(owner); w.Write(ai);
            }

            public override void Deserialize(NetworkReader r)
            {
                kind = r.ReadByte(); lua = r.ReadString(); note = r.ReadString(); faction = r.ReadUInt32(); sel = r.ReadUInt32();
                hasPos = r.ReadBoolean(); pos = r.ReadVector3(); force = r.ReadSingle(); owner = r.ReadUInt32(); ai = r.ReadBoolean();
                if (note.Length > 120) note = note.Substring(0, 120);
            }
        }

        internal sealed class NoteMsg : MessageBase
        {
            internal string who, note;
            internal uint actor;
            public override void Serialize(NetworkWriter w) { w.Write(who ?? ""); w.Write(note ?? ""); w.Write(actor); }
            public override void Deserialize(NetworkReader r) { who = r.ReadString(); note = r.ReadString(); actor = r.ReadUInt32(); }
        }

        // ------------------------------------------------------------------ host
        private static float _rateT;
        private static int _rateN;

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                _pending.Clear();
                _noteLast.Clear();
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgSpawn);
                    NetworkServer.RegisterHandler(MsgSpawn, OnSpawnAtHost);
                    NetworkServer.UnregisterHandler(MsgAct);
                    NetworkServer.RegisterHandler(MsgAct, OnActAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null && !NetworkServer.active)
                {
                    c.UnregisterHandler(MsgNote);
                    c.RegisterHandler(MsgNote, OnNoteAtClient);
                }
                if (NetworkServer.active) BtcMovieTime.ResetHost(); else BtcMovieTime.RegisterClient(c);
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
                // any faction the sidebar picked (also another player's); none -> the requesting player's own faction
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (f == null && u != null && u.faction != null) f = u.faction;
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
                if (d != null) Announce(u, "spawn " + msg.key.Name);
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
