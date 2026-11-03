using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Assets;
using Brix.Game;
using Brix.Lua;
using Brix.Network;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Lobby spawn selection (BetterConnection ON only): the host assigns a "Player Starting Point" marker to a lobby
    /// faction slot (LobbyPlayer.factionNumber); clients may request a marker for their own slot. The host's table is
    /// applied at match start by wrapping the original conquest DetermineCrystals / sandbox start pick in the server Lua
    /// (Asset_Gamemode.Disk_LoadGamemode Postfix). Unassigned slots keep the vanilla logic.
    /// Wire (mod ids, BC ON only): 65 state host->clients (display), 66 request client->host.
    /// Markers are identified by their map position (x,z), never by index.
    /// </summary>
    internal static class BtcSpawnPick
    {
        internal const short MsgState = 65;
        internal const short MsgRequest = 66;
        internal const string StartGroup = "Player Starting Point";

        // factionNumber -> marker position (x, z)
        internal static readonly Dictionary<int, Vector2> Assign = new Dictionary<int, Vector2>();
        internal static readonly Dictionary<int, Vector2> Requests = new Dictionary<int, Vector2>();
        private static string _mapKey;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgRequest);
                    NetworkServer.RegisterHandler(MsgRequest, OnRequestAtHost);
                }
                else
                {
                    Assign.Clear();
                    Requests.Clear();
                    _mapKey = null;
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "RegisterHandlers: " + e); }
        }

        internal static void SendState(NetworkConnection conn)
        {
            if (conn == null || !BtcNetworkGate.Enabled || !NetworkServer.active) return;
            try { conn.Send(MsgState, new StringMessage(Encode())); }
            catch (Exception e) { BtcLog.Error("SpawnPick", "SendState: " + e); }
        }

        private static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringMessage msg = new StringMessage(Encode());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "Broadcast: " + e); }
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
                Decode(msg.value);
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "OnStateAtClient: " + e); }
        }

        private static void OnRequestAtHost(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (!NetworkServer.active || !BtcNetworkGate.Enabled || !BtcGameplay.InLobby) return;
                LobbyPlayer p = PlayerOf(nm.conn);
                if (p == null) return;
                string v = msg.value ?? string.Empty;
                if (v == "clear")
                {
                    Requests.Remove(p.factionNumber);
                }
                else
                {
                    string[] a = v.Split(';');
                    float x, z;
                    if (a.Length != 2 || !float.TryParse(a[0], NumberStyles.Float, Inv, out x) || !float.TryParse(a[1], NumberStyles.Float, Inv, out z)) return;
                    Requests[p.factionNumber] = new Vector2(x, z);
                }
                BtcLog.Info("SpawnPick", "request from slot " + p.factionNumber + " (" + p.playerName + "): " + v);
                Broadcast();
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "OnRequestAtHost: " + e); }
        }

        /// <summary>Client: ask the host for a marker (null = withdraw the request).</summary>
        internal static void SendRequest(Vector2? pos)
        {
            if (!BtcNetworkGate.Enabled || NetworkServer.active) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = m != null ? m.LocalNetworkClient : null;
                if (c == null || !c.isConnected) return;
                string v = pos.HasValue ? pos.Value.x.ToString("R", Inv) + ";" + pos.Value.y.ToString("R", Inv) : "clear";
                c.Send(MsgRequest, new StringMessage(v));
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "SendRequest: " + e); }
        }

        // ------------------------------------------------------------------ host edits
        internal static void HostAssign(int faction, Vector2? pos)
        {
            if (!BtcGameplay.CanEdit) return;
            if (pos.HasValue)
            {
                // one marker per slot: a marker taken by another slot moves to this one
                List<int> drop = null;
                foreach (KeyValuePair<int, Vector2> kv in Assign)
                {
                    if (kv.Key != faction && Same(kv.Value, pos.Value)) (drop ?? (drop = new List<int>())).Add(kv.Key);
                }
                if (drop != null) for (int i = 0; i < drop.Count; i++) Assign.Remove(drop[i]);
                Assign[faction] = pos.Value;
            }
            else
            {
                Assign.Remove(faction);
            }
            Requests.Remove(faction);
            BtcLog.Info("SpawnPick", "host: slot " + faction + " -> " + (pos.HasValue ? pos.Value.ToString() : "vanilla"));
            Broadcast();
        }

        internal static void HostApprove(int faction)
        {
            Vector2 p;
            if (Requests.TryGetValue(faction, out p)) HostAssign(faction, p);
        }

        internal static void HostReject(int faction)
        {
            if (!BtcGameplay.CanEdit) return;
            if (Requests.Remove(faction)) Broadcast();
        }

        internal static void HostClearAll()
        {
            if (!BtcGameplay.CanEdit) return;
            Assign.Clear();
            Requests.Clear();
            Broadcast();
        }

        /// <summary>Host, once per frame: forget the table when the selected map changes.</summary>
        internal static void Tick()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || !BtcGameplay.InLobby) return;
            Asset_Map map = GameParam.Map;
            string key = map != null ? map.path : null;
            if (ReferenceEquals(key, _mapKey) || key == _mapKey) return;
            bool had = _mapKey != null && (Assign.Count > 0 || Requests.Count > 0);
            _mapKey = key;
            if (had)
            {
                Assign.Clear();
                Requests.Clear();
                BtcLog.Info("SpawnPick", "map changed: table cleared");
                Broadcast();
            }
        }

        internal static bool Same(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }

        internal static LobbyPlayer PlayerOf(NetworkConnection conn)
        {
            UNetManager m = UNetManager.instance;
            if (m == null || conn == null || m.LobbyPlayerList == null) return null;
            for (int i = 0; i < m.LobbyPlayerList.Count; i++)
            {
                LobbyPlayer p = m.LobbyPlayerList[i];
                if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId == conn.connectionId) return p;
            }
            return null;
        }

        internal static LobbyPlayer LocalPlayer()
        {
            UNetManager m = UNetManager.instance;
            if (m == null || m.LobbyPlayerList == null) return null;
            for (int i = 0; i < m.LobbyPlayerList.Count; i++)
            {
                LobbyPlayer p = m.LobbyPlayerList[i];
                if (p != null && p.isLocalPlayer) return p;
            }
            return null;
        }

        private static string Encode()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, Vector2> kv in Assign)
                sb.Append("A;").Append(kv.Key).Append(';').Append(kv.Value.x.ToString("R", Inv)).Append(';').Append(kv.Value.y.ToString("R", Inv)).Append('\n');
            foreach (KeyValuePair<int, Vector2> kv in Requests)
                sb.Append("R;").Append(kv.Key).Append(';').Append(kv.Value.x.ToString("R", Inv)).Append(';').Append(kv.Value.y.ToString("R", Inv)).Append('\n');
            return sb.ToString();
        }

        private static void Decode(string s)
        {
            Assign.Clear();
            Requests.Clear();
            if (string.IsNullOrEmpty(s)) return;
            string[] lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] a = lines[i].Split(';');
                int f;
                float x, z;
                if (a.Length != 4 || !int.TryParse(a[1], NumberStyles.Integer, Inv, out f)
                    || !float.TryParse(a[2], NumberStyles.Float, Inv, out x) || !float.TryParse(a[3], NumberStyles.Float, Inv, out z)) continue;
                if (a[0] == "A") Assign[f] = new Vector2(x, z);
                else if (a[0] == "R") Requests[f] = new Vector2(x, z);
            }
        }

        // ------------------------------------------------------------------ match start (host, server Lua)
        // factionNumber -> marker GameObject instance id, resolved once per gamemode load.
        private static Dictionary<int, int> _resolved;
        private static HashSet<int> _reserved;

        private const string LuaConquest =
            "if DetermineCrystals ~= nil and __BtcSpawnHas ~= nil then\n" +
            " local __btc_dc = DetermineCrystals\n" +
            " DetermineCrystals = function(fs)\n" +
            "  __btc_dc(fs)\n" +
            "  local ok, err = pcall(function()\n" +
            "   for i = 1, #fs do\n" +
            "    local f = fs[i]\n" +
            "    if __BtcSpawnHas(f) then\n" +
            "     local want, cur = nil, nil\n" +
            "     for _, s in pairs(dc_spawnPoints) do\n" +
            "      if __BtcSpawnIs(f, s) then want = s end\n" +
            "      if s.desiredFaction == f then cur = s end\n" +
            "     end\n" +
            "     if want ~= nil and want ~= cur then\n" +
            "      local g = want.desiredFaction\n" +
            "      if cur ~= nil then\n" +
            "       want.desiredFaction = f\n" +
            "       cur.desiredFaction = g\n" +
            "      elseif g == Faction.defaultNeutral then\n" +
            "       want.desiredFaction = f\n" +
            "      end\n" +
            "     end\n" +
            "    end\n" +
            "   end\n" +
            "  end)\n" +
            "  if not ok then __BtcSpawnLog(tostring(err)) else __BtcSpawnLog('conquest: applied') end\n" +
            " end\n" +
            "end\n";

        // sandbox: the start pick is `table.remove(spawnPoints, math.random(#spawnPoints))` inside a local function,
        // once per owned faction in Faction:GetAll() order. Wrap Markers.GetGroup + table.remove for that one table.
        private const string LuaSandbox =
            "if Markers ~= nil and Markers.GetGroup ~= nil and __BtcSpawnHas ~= nil then\n" +
            " local __btc_gg = Markers.GetGroup\n" +
            " local __btc_rm = table.remove\n" +
            " local __btc_t = nil\n" +
            " local __btc_k = 0\n" +
            " Markers.GetGroup = function(name)\n" +
            "  local t = __btc_gg(name)\n" +
            "  if name == 'Player Starting Point' then __btc_t = t; __btc_k = 0 end\n" +
            "  return t\n" +
            " end\n" +
            " local function pick(t, i)\n" +
            "  __btc_k = __btc_k + 1\n" +
            "  local n, fac = 0, nil\n" +
            "  for _, f in pairs(Faction:GetAll()) do\n" +
            "   if f:HasOwner() then n = n + 1; if n == __btc_k then fac = f; break end end\n" +
            "  end\n" +
            "  if fac ~= nil and __BtcSpawnHas(fac) then\n" +
            "   for j = 1, #t do if __BtcSpawnIs(fac, t[j]) then return j end end\n" +
            "  end\n" +
            "  if t[i] ~= nil and __BtcSpawnReserved(t[i]) then\n" +
            "   local free = {}\n" +
            "   for j = 1, #t do if not __BtcSpawnReserved(t[j]) then free[#free + 1] = j end end\n" +
            "   if #free > 0 then return free[math.random(#free)] end\n" +
            "  end\n" +
            "  return i\n" +
            " end\n" +
            " table.remove = function(t, i)\n" +
            "  if i ~= nil and t ~= nil and t == __btc_t then\n" +
            "   local ok, r = pcall(pick, t, i)\n" +
            "   if ok and r ~= nil then i = r else __BtcSpawnLog(tostring(r)) end\n" +
            "  end\n" +
            "  if i == nil then return __btc_rm(t) end\n" +
            "  return __btc_rm(t, i)\n" +
            " end\n" +
            "end\n";

        /// <summary>Asset_Gamemode.Disk_LoadGamemode Postfix (server script). Host + BC ON + new game + table not empty.</summary>
        internal static void OnGamemodeLoaded(Asset_Gamemode gm, Script script)
        {
            if (script == null || gm == null || !NetworkServer.active || !BtcNetworkGate.Enabled) return;
            if (Assign.Count == 0) return;
            Asset_Map map = GameParam.Map;
            if (map == null || !map.IsNewGame()) return;
            string folder = Path.GetFileName((gm.path ?? string.Empty).TrimEnd('/', '\\'));
            // original gamemodes only (never the foreign "conquest mod")
            string code;
            if (folder == "conquest") code = LuaConquest;
            else if (folder == "sandbox") code = LuaSandbox;
            else
            {
                BtcLog.Info("SpawnPick", "gamemode '" + folder + "' not supported, vanilla starts");
                return;
            }
            _resolved = null;
            _reserved = null;
            script.Globals["__BtcSpawnHas"] = DynValue.NewCallback(LuaHas, null);
            script.Globals["__BtcSpawnIs"] = DynValue.NewCallback(LuaIs, null);
            script.Globals["__BtcSpawnReserved"] = DynValue.NewCallback(LuaReserved, null);
            script.Globals["__BtcSpawnLog"] = DynValue.NewCallback(LuaLog, null);
            script.DoString(code, null, "btc_spawnpick");
            BtcLog.Info("SpawnPick", "installed for " + folder + ", " + Assign.Count + " assigned slot(s)");
        }

        private static void Resolve()
        {
            if (_resolved != null) return;
            _resolved = new Dictionary<int, int>();
            _reserved = new HashSet<int>();
            List<LuaMarker> ms = LuaMarker.GetGroup(StartGroup);
            if (ms == null || ms.Count == 0) return;
            HashSet<int> used = new HashSet<int>();
            foreach (KeyValuePair<int, Vector2> kv in Assign)
            {
                int best = -1;
                float bd = float.MaxValue;
                for (int i = 0; i < ms.Count; i++)
                {
                    if (ms[i] == null) continue;
                    int id = ms[i].gameObject.GetInstanceID();
                    if (used.Contains(id)) continue;
                    Vector3 p = ms[i].transform.position;
                    float d = (p.x - kv.Value.x) * (p.x - kv.Value.x) + (p.z - kv.Value.y) * (p.z - kv.Value.y);
                    if (d < bd) { bd = d; best = i; }
                }
                if (best < 0) continue;
                int bid = ms[best].gameObject.GetInstanceID();
                used.Add(bid);
                _resolved[kv.Key] = bid;
                _reserved.Add(bid);
                BtcLog.Info("SpawnPick", "slot " + kv.Key + " -> marker at " + ms[best].transform.position + " (dist " + Mathf.Sqrt(bd).ToString("0.0", Inv) + ")");
            }
        }

        internal static object Obj(CallbackArguments args, int i)
        {
            if (args.Count <= i) return null;
            DynValue v = args[i];
            return v.Type == DataType.UserData ? v.ToObject<object>() : null;
        }

        private static int FactionNumber(object o)
        {
            if (o == null) return -1;
            Faction f = o as Faction;
            if (f == null)
            {
                Component c = o as Component;
                if (c != null) f = c.GetComponent<Faction>();
            }
            if (f == null || f.AllOwners == null) return -1;
            for (int i = 0; i < f.AllOwners.Count; i++)
            {
                if (f.AllOwners[i] != null) return f.AllOwners[i].factionNumber;
            }
            return -1;
        }

        private static int ObjId(object o)
        {
            Component c = o as Component;
            return c != null ? c.gameObject.GetInstanceID() : 0;
        }

        private static DynValue LuaHas(ScriptExecutionContext ctx, CallbackArguments args)
        {
            try
            {
                Resolve();
                int fn = FactionNumber(Obj(args, 0));
                return DynValue.NewBoolean(fn >= 0 && _resolved.ContainsKey(fn));
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "Has: " + e); return DynValue.False; }
        }

        private static DynValue LuaIs(ScriptExecutionContext ctx, CallbackArguments args)
        {
            try
            {
                Resolve();
                int fn = FactionNumber(Obj(args, 0));
                int id;
                return DynValue.NewBoolean(fn >= 0 && args.Count > 1 && _resolved.TryGetValue(fn, out id) && id == ObjId(Obj(args, 1)));
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "Is: " + e); return DynValue.False; }
        }

        private static DynValue LuaReserved(ScriptExecutionContext ctx, CallbackArguments args)
        {
            try
            {
                Resolve();
                return DynValue.NewBoolean(args.Count > 0 && _reserved.Contains(ObjId(Obj(args, 0))));
            }
            catch (Exception e) { BtcLog.Error("SpawnPick", "Reserved: " + e); return DynValue.False; }
        }

        private static DynValue LuaLog(ScriptExecutionContext ctx, CallbackArguments args)
        {
            try { BtcLog.Info("SpawnPick", "lua: " + (args.Count > 0 ? args[0].ToPrintString() : string.Empty)); }
            catch (Exception) { }
            return DynValue.Void;
        }
    }
}
