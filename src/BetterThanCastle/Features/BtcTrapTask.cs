using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.InstructionType;
using Brix.Game.AI.Nodes;
using Brix.Game.Beacons;
using Brix.Game.Components;
using Brix.Game.Network;
using Brix.Lifecycle.Pooling;
using Brix.Network;
using Brix.UI.Icons;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Bear traps task (rule work.trapTask, host rule, BetterConnection ON). Re-arming fired traps becomes a real task with priority and
    /// crew like any other: a vanilla Repair task (Groupes.Repair) carrying a set of traps, so priority, crew, delete, task bar and save
    /// stay vanilla; Patches/TrapTaskPatches.cs swaps its work (HasWorkFor / GetInstruction / GoalCounts / VerifyWorkFor) and turns off
    /// the vanilla self-standing trap job (ObjectActivator.HasWorkFor). Every faction with a placed trap gets one automatic task holding
    /// all its traps that are in no own task; the player can make own tasks for groups of traps (frame / click, BtcTrapTaskTool) and a
    /// trap then leaves the automatic task. A worker reserves one fired trap of the task while he runs to it (nobody else takes it),
    /// takes the nearest free one, and first stores whatever he carries in a fitting container; with something in the hands and no
    /// container he does not take the job at all. The manual re-arm (builder click on a trap) stays vanilla.
    /// Wire: msg 142 client->host (create / add / remove), msg 143 host->own-faction clients (tasks and their traps).
    /// Save sidecar btc-traptasks.txt (beacon position + trap positions), matched back after the load.
    /// </summary>
    internal static class BtcTrapTask
    {
        internal const short MsgReq = 142;
        internal const short MsgState = 143;
        private const string FileName = "btc-traptasks.txt";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _iOn = -2;

        internal static void AddRows()
        {
            BtcGameplay.Add("work.trapTask", "eco", "Bear traps task", "Задание: капканы", 1f, 0f, 1f, 1f, true,
                "Re-arming fired bear traps is a task in the task list (trap icon) with its own priority and crew: set it to the lowest priority to stop it. It appears with the first placed trap; you can also make own trap tasks for groups of traps (New task > Bear traps). Each worker re-arms his own trap (no crowd at one trap) and first stores what he carries, so no ingots or planks are dropped at the trap. Needs the C# game menu for making own tasks.",
                "Перезарядка сработавших капканов стала заданием в списке задач (иконка капкана) со своим приоритетом и числом рабочих: поставь самый низкий приоритет, чтобы её остановить. Задание появляется с первым поставленным капканом; можно делать свои задания на группы капканов (Новое задание > Капканы). Каждый рабочий перезаряжает свой капкан (никакой толпы у одного капкана) и сначала кладёт на склад то, что несёт, так что у капкана не остаются слитки и доски. Свои задания делаются через C# меню игры.");
        }

        internal static bool On
        {
            get
            {
                if (!BtcNetworkGate.Enabled) return false;
                if (_iOn == -2) _iOn = BtcGameplay.Idx("work.trapTask");
                float[] a = BtcGameplay.Active;
                return _iOn >= 0 && a != null && _iOn < a.Length && a[_iOn] > 0.5f;
            }
        }

        // ------------------------------------------------------------------ model (host and clients)
        internal sealed class Info
        {
            internal GameObject Go;
            internal uint Nid;
            internal bool Auto;
            internal int Faction = -1;
            internal readonly HashSet<uint> Traps = new HashSet<uint>();   // custom: the chosen traps; auto: computed (host) / sent (client)
        }

        internal static readonly Dictionary<int, Info> ByObj = new Dictionary<int, Info>();
        internal static readonly Dictionary<uint, Info> ByNet = new Dictionary<uint, Info>();
        private static readonly List<uint> _tmp = new List<uint>();

        internal static Info Of(Component c)
        {
            if (ByObj.Count == 0 || c == null) return null;
            Info i;
            return ByObj.TryGetValue(c.gameObject.GetInstanceID(), out i) ? i : null;
        }

        /// <summary>Trap record of the 1 Hz scan (host: all factions; client: only to draw / pick).</summary>
        internal struct Trap
        {
            internal ObjectActivator A;
            internal BearTrapTrigger T;
            internal uint Nid;
            internal int Faction;
            internal Vector3 Pos;
            internal bool Fired;
        }

        /// <summary>Placed traps (not carried, not stored), refreshed once a second.</summary>
        internal static readonly List<Trap> Traps = new List<Trap>();
        private static readonly Dictionary<uint, int> TrapIdx = new Dictionary<uint, int>();
        private static readonly Dictionary<uint, Info> Owner = new Dictionary<uint, Info>();   // host: trap -> its task
        private static float _nextScan;

        private static readonly FieldInfo ShutField = AccessTools.Field(typeof(BearTrapTrigger), "isShut");
        private static readonly MethodInfo OperateMi = AccessTools.Method(typeof(ObjectActivator), "Operate");

        internal static bool TryTrap(uint nid, out Trap t)
        {
            int i;
            if (TrapIdx.TryGetValue(nid, out i) && i < Traps.Count) { t = Traps[i]; return true; }
            t = default(Trap);
            return false;
        }

        private static void Scan()
        {
            Traps.Clear(); TrapIdx.Clear();
            BearTrapTrigger[] all = BtcRegistry.TrapsArray();   // not a scene walk every second
            bool host = NetworkServer.active;
            for (int i = 0; i < all.Length; i++)
            {
                BearTrapTrigger t = all[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                ObjectActivator a = t.GetComponent<ObjectActivator>();
                NetworkIdentity ni = t.GetComponent<NetworkIdentity>();
                if (a == null || ni == null || ni.netId.Value == 0 || a.IsInRecepteur()) continue;   // carried or stored: not a placed trap
                Faction f = t.faction;
                Trap r;
                r.A = a; r.T = t; r.Nid = ni.netId.Value; r.Faction = f != null ? f.ID : -1; r.Pos = t.transform.position;
                if (host) r.Fired = a.Active != a.DesiredState;
                else { object v = ShutField != null ? ShutField.GetValue(t) : null; r.Fired = v != null && Convert.ToInt32(v) != 1; }   // TrapState.open = 1
                TrapIdx[r.Nid] = Traps.Count;
                Traps.Add(r);
            }
        }

        // ------------------------------------------------------------------ lifecycle
        internal static void ClearAll()
        {
            ByObj.Clear(); ByNet.Clear(); Owner.Clear(); Res.Clear(); Traps.Clear(); TrapIdx.Clear();
            _sent.Clear(); _pending = null; _storeCache.Clear();
        }

        /// <summary>Update (ResourcePackManager): 1 Hz scan, prune gone tasks, host: keep the automatic tasks, membership, state to clients.</summary>
        internal static void Tick()
        {
            try
            {
                float now = Time.unscaledTime;
                if (now < _nextScan) return;
                _nextScan = now + 1f;
                if (!NetworkServer.active && !NetworkClient.active) { if (ByNet.Count > 0 || Traps.Count > 0) ClearAll(); return; }
                if (!On) { if (ByNet.Count > 0) ClearAll(); return; }
                bool host = NetworkServer.active;
                if (!host && ByNet.Count == 0 && !BtcTrapTaskTool.Active) return;   // a client scans only for its own tasks / the tool
                Scan();
                if (!host) { ResolveClient(); return; }
                if (_pending != null) ApplyPending(now);
                PruneHost();
                if (_pending == null) KeepAuto();   // a loaded save first gets its own tasks back
                Membership();
                SendChanged(null);
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "Tick: " + e); }
        }

        private static bool Alive(Info e)
        {
            if (e.Go == null || !e.Go.activeInHierarchy) return false;
            NetworkIdentity ni = e.Go.GetComponent<NetworkIdentity>();
            return ni != null && ni.netId.Value == e.Nid;
        }

        private static void Unbind(uint nid)
        {
            Info e;
            if (!ByNet.TryGetValue(nid, out e)) return;
            if (e.Go != null) { ByObj.Remove(e.Go.GetInstanceID()); BtcTerraform.BeaconLook(e.Go, false, null, null); }
            ByNet.Remove(nid);
            BtcLog.Info("TrapTask", "task " + nid + " gone");
        }

        private static void PruneHost()
        {
            _tmp.Clear();
            foreach (KeyValuePair<uint, Info> kv in ByNet) if (!Alive(kv.Value)) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) Unbind(_tmp[i]);
            // custom tasks lose traps that are gone (picked up, destroyed); an emptied custom task is deleted
            _tmp.Clear();
            foreach (KeyValuePair<uint, Info> kv in ByNet)
            {
                Info e = kv.Value;
                if (e.Auto) continue;
                _rm.Clear();
                foreach (uint t in e.Traps) { Trap r; if (!TryTrap(t, out r) || r.Faction != e.Faction) _rm.Add(t); }
                for (int i = 0; i < _rm.Count; i++) e.Traps.Remove(_rm[i]);
                if (e.Traps.Count == 0) _tmp.Add(kv.Key);
            }
            for (int i = 0; i < _tmp.Count; i++) Delete(_tmp[i], "no traps left");
        }

        private static readonly List<uint> _rm = new List<uint>();
        private static readonly HashSet<int> _factions = new HashSet<int>();

        /// <summary>Host: every faction with a placed trap has one automatic task; without traps it is removed.</summary>
        private static void KeepAuto()
        {
            _factions.Clear();
            for (int i = 0; i < Traps.Count; i++) if (Traps[i].Faction >= 0) _factions.Add(Traps[i].Faction);
            _tmp.Clear();
            foreach (KeyValuePair<uint, Info> kv in ByNet) if (kv.Value.Auto && !_factions.Contains(kv.Value.Faction)) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) Delete(_tmp[i], "no traps");
            foreach (int f in _factions)
            {
                if (AutoOf(f) != null) continue;
                User u = UserOf(f);
                if (u == null) continue;
                Spawn(u, true, null);
            }
        }

        internal static Info AutoOf(int faction)
        {
            foreach (KeyValuePair<uint, Info> kv in ByNet) if (kv.Value.Auto && kv.Value.Faction == faction) return kv.Value;
            return null;
        }

        private static User UserOf(int faction)
        {
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.UserList == null) return null;
            for (int i = 0; i < m.UserList.Count; i++)
            {
                User u = m.UserList[i];
                if (u != null && u.faction != null && u.faction.ID == faction && u.GetComponent<UNetGroupe>() != null) return u;
            }
            return null;
        }

        /// <summary>Host: trap -> task (custom first, else the faction's automatic task); auto tasks list their traps for the clients.</summary>
        private static void Membership()
        {
            Owner.Clear();
            foreach (KeyValuePair<uint, Info> kv in ByNet)
            {
                if (kv.Value.Auto) continue;
                foreach (uint t in kv.Value.Traps) Owner[t] = kv.Value;
            }
            foreach (KeyValuePair<uint, Info> kv in ByNet) if (kv.Value.Auto) kv.Value.Traps.Clear();
            for (int i = 0; i < Traps.Count; i++)
            {
                Trap r = Traps[i];
                if (Owner.ContainsKey(r.Nid)) continue;
                Info a = AutoOf(r.Faction);
                if (a == null) continue;
                a.Traps.Add(r.Nid);
                Owner[r.Nid] = a;
            }
            // the beacon follows its traps (centre, above the highest), so the task shows where its traps are
            foreach (KeyValuePair<uint, Info> kv in ByNet) PlaceBeacon(kv.Value, false);
        }

        private static void PlaceBeacon(Info e, bool force)
        {
            Vector3 c; if (!Centre(e, out c)) return;
            BeaconDriver d = e.Go != null ? e.Go.GetComponent<BeaconDriver>() : null;
            if (d == null) return;
            if (!force && (d.NetworkNetworkPosition - c).sqrMagnitude < 4f) return;
            d.NetworkNetworkPosition = c;
        }

        private static bool Centre(Info e, out Vector3 c)
        {
            c = Vector3.zero;
            int n = 0; float top = float.MinValue;
            foreach (uint t in e.Traps)
            {
                Trap r; if (!TryTrap(t, out r)) continue;
                c += r.Pos; n++;
                if (r.Pos.y > top) top = r.Pos.y;
            }
            if (n == 0) return false;
            c /= n; c.y = top + 1f;
            return true;
        }

        // ------------------------------------------------------------------ host: spawn / delete / edit
        private static GameObject Spawn(User u, bool auto, ICollection<uint> traps)
        {
            UNetGroupe ug = u.GetComponent<UNetGroupe>();
            if (ug == null) return null;
            Info e = new Info { Auto = auto, Faction = u.faction.ID };
            if (traps != null) foreach (uint t in traps) e.Traps.Add(t);
            Vector3 pos;
            if (!Centre(e, out pos))
            {
                // auto task: the centre of all the faction's traps
                int n = 0; pos = Vector3.zero; float top = float.MinValue;
                for (int i = 0; i < Traps.Count; i++) if (Traps[i].Faction == e.Faction) { pos += Traps[i].Pos; n++; top = Mathf.Max(top, Traps[i].Pos.y); }
                if (n == 0) return null;
                pos /= n; pos.y = top + 1f;
            }
            GameObject go = ug.CreateGroup(Groupes.Repair, pos, false);
            BeaconDriver d = go.GetComponent<BeaconDriver>();
            if (d != null) d.NetworkNetworkRadius = 1;   // the repair walk of a trap task covers almost nothing (its goals are muted anyway)
            Project pr = go.GetComponent<Project>();
            ByObj[go.GetInstanceID()] = e;   // before Spawn: the providers' first walk already sees the task as a trap task
            NetworkServer.Spawn(go, go.GetComponent<AssetKeyHolder>().AssetKey.AssetId);
            e.Go = go;
            e.Nid = go.GetComponent<NetworkIdentity>().netId.Value;
            ByNet[e.Nid] = e;
            if (pr != null) { pr.AutoDeleteDisabled = true; pr.Commit(); }
            OnBound(e);
            BtcLog.Info("TrapTask", (auto ? "auto" : "own") + " task " + e.Nid + " created for faction " + e.Faction + " (" + e.Traps.Count + " traps)");
            return go;
        }

        private static void Delete(uint nid, string why)
        {
            Info e;
            if (!ByNet.TryGetValue(nid, out e)) return;
            GameObject go = e.Go;
            Unbind(nid);
            if (go != null && !go.IsNullOrReleased())
            {
                NetworkServer.UnSpawn(go);
                ObjectPoolSingleton.Release(go);
            }
            BtcLog.Info("TrapTask", "task " + nid + " deleted: " + why);
            _dirty = true;
        }

        private static void OnReqAtHost(NetworkMessage nm)
        {
            try
            {
                string text = nm.ReadMessage<StringMessage>().value;
                if (string.IsNullOrEmpty(text) || !On || !NetworkServer.active) return;
                UNetManager m = NetworkManager.singleton as UNetManager;
                User u = m == null ? null : (m.debugLoader ? User.LocalUser : m.UserList.Find(x => x != null && x.connectionToClient != null && x.connectionToClient.connectionId == nm.conn.connectionId));
                if (u == null || u.faction == null) { BtcLog.Warn("TrapTask", "request: no user for connection"); return; }
                Request(u, text);
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "OnReqAtHost: " + e); }
        }

        /// <summary>Host: "C t1 t2 .." new own task, "A nid t1 .." add traps to a task (to the auto task = back from own tasks),
        /// "R nid t1 .." remove traps from an own task (they go back to the auto task).</summary>
        private static void Request(User u, string text)
        {
            string[] p = text.Split(' ');
            if (p.Length < 2) return;
            Scan();
            Membership();
            int f = u.faction.ID;
            int start = p[0] == "C" ? 1 : 2;
            List<uint> traps = new List<uint>();
            for (int i = start; i < p.Length; i++)
            {
                uint t; Trap r;
                if (uint.TryParse(p[i], NumberStyles.Integer, Inv, out t) && TryTrap(t, out r) && r.Faction == f) traps.Add(t);
            }
            if (traps.Count == 0) return;
            if (p[0] == "C")
            {
                foreach (KeyValuePair<uint, Info> kv in ByNet) if (!kv.Value.Auto) for (int i = 0; i < traps.Count; i++) kv.Value.Traps.Remove(traps[i]);
                Spawn(u, false, traps);
            }
            else
            {
                uint nid; Info e;
                if (!uint.TryParse(p[1], NumberStyles.Integer, Inv, out nid) || !ByNet.TryGetValue(nid, out e) || e.Faction != f) { BtcLog.Warn("TrapTask", "request: task " + p[1] + " not of faction " + f); return; }
                if (p[0] == "A")
                {
                    foreach (KeyValuePair<uint, Info> kv in ByNet) if (!kv.Value.Auto && kv.Value != e) for (int i = 0; i < traps.Count; i++) kv.Value.Traps.Remove(traps[i]);
                    if (!e.Auto) for (int i = 0; i < traps.Count; i++) e.Traps.Add(traps[i]);
                }
                else if (p[0] == "R" && !e.Auto)
                    for (int i = 0; i < traps.Count; i++) e.Traps.Remove(traps[i]);
                else return;
                BtcLog.Info("TrapTask", "task " + nid + " " + p[0] + " " + traps.Count + " trap(s)");
            }
            PruneHost();
            Membership();
            foreach (KeyValuePair<uint, Info> kv in ByNet) PlaceBeacon(kv.Value, true);
            _dirty = true;
            SendChanged(null);
        }

        /// <summary>Client side (also the host's own UI): send a request (see Request).</summary>
        internal static void Send(string text)
        {
            if (!On) return;
            if (NetworkServer.active && User.LocalUser != null) { Request(User.LocalUser, text); return; }
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.LocalNetworkClient == null || !m.LocalNetworkClient.isConnected) return;
            m.LocalNetworkClient.Send(MsgReq, new StringMessage(text));
            BtcLog.Info("TrapTask", "request " + (text.Length > 60 ? text.Substring(0, 60) + ".." : text));
        }

        // ------------------------------------------------------------------ host -> clients ("nid a t1 t2 ..;" per task of the client's faction)
        private static readonly Dictionary<int, string> _sent = new Dictionary<int, string>();   // connection id -> last state text
        private static bool _dirty;

        private static string StateFor(int faction)
        {
            StringBuilder sb = new StringBuilder("S");
            foreach (KeyValuePair<uint, Info> kv in ByNet)
            {
                Info e = kv.Value;
                if (e.Faction != faction) continue;
                sb.Append(e.Nid.ToString(Inv)).Append(' ').Append(e.Auto ? '1' : '0');
                foreach (uint t in e.Traps) sb.Append(' ').Append(t.ToString(Inv));
                sb.Append(';');
            }
            return sb.ToString();
        }

        private static void SendChanged(NetworkConnection only)
        {
            if (!NetworkServer.active) return;
            UNetManager m = NetworkManager.singleton as UNetManager;
            if (m == null || m.UserList == null) return;
            for (int i = 0; i < m.UserList.Count; i++)
            {
                User u = m.UserList[i];
                NetworkConnection c = u != null ? u.connectionToClient : null;
                if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c) || u.faction == null) continue;
                if (only != null && c != only) continue;
                string text = StateFor(u.faction.ID);
                string last;
                if (only == null && !_dirty && _sent.TryGetValue(c.connectionId, out last) && last == text) continue;
                _sent[c.connectionId] = text;
                if (text.Length > 30000) text = text.Substring(0, 30000);
                c.Send(MsgState, new StringMessage(text));
            }
            _dirty = false;
        }

        /// <summary>Host: a client became ready, send it its tasks.</summary>
        internal static void SendState(NetworkConnection conn)
        {
            if (!NetworkServer.active || !On || conn == null) return;
            try { _sent.Remove(conn.connectionId); SendChanged(conn); }
            catch (Exception e) { BtcLog.Error("TrapTask", "SendState: " + e.Message); }
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                string text = nm.ReadMessage<StringMessage>().value;
                if (NetworkServer.active || string.IsNullOrEmpty(text) || text[0] != 'S') return;
                HashSet<uint> seen = new HashSet<uint>();
                foreach (string part in text.Substring(1).Split(';'))
                {
                    string[] p = part.Split(' ');
                    uint nid;
                    if (p.Length < 2 || !uint.TryParse(p[0], NumberStyles.Integer, Inv, out nid)) continue;
                    seen.Add(nid);
                    Info e;
                    if (!ByNet.TryGetValue(nid, out e)) { e = new Info { Nid = nid }; ByNet[nid] = e; }
                    e.Auto = p[1] == "1";
                    e.Faction = User.LocalUser != null && User.LocalUser.faction != null ? User.LocalUser.faction.ID : -1;
                    e.Traps.Clear();
                    for (int i = 2; i < p.Length; i++) { uint t; if (uint.TryParse(p[i], NumberStyles.Integer, Inv, out t)) e.Traps.Add(t); }
                }
                _tmp.Clear();
                foreach (KeyValuePair<uint, Info> kv in ByNet) if (!seen.Contains(kv.Key)) _tmp.Add(kv.Key);
                for (int i = 0; i < _tmp.Count; i++) Unbind(_tmp[i]);
                ResolveClient();
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "OnStateAtClient: " + e.Message); }
        }

        // client: late spawns of the task objects
        private static void ResolveClient()
        {
            foreach (KeyValuePair<uint, Info> kv in ByNet)
            {
                Info e = kv.Value;
                if (e.Go != null && Alive(e)) continue;
                if (e.Go != null) ByObj.Remove(e.Go.GetInstanceID());
                e.Go = ClientScene.FindLocalObject(new NetworkInstanceId(kv.Key));
                if (e.Go == null) continue;
                ByObj[e.Go.GetInstanceID()] = e;
                OnBound(e);
            }
        }

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgReq);
                    NetworkServer.RegisterHandler(MsgReq, OnReqAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
            }
            catch (Exception e) { BtcLog.Error("TrapTask", "RegisterHandlers: " + e); }
        }

        // ------------------------------------------------------------------ look
        private static Sprite _icon;
        internal static Sprite Icon
        {
            get
            {
                // the build wheel icon (Meta_Structure.lua); IconKeys.BearTrap ("Items") is an old sprite with a "deprecated" label
                if (_icon == null) { try { _icon = IconKeys._Beartrap.Get64(); } catch (Exception e) { BtcLog.Error("TrapTask", "icon: " + e.Message); } }
                return _icon;
            }
        }

        internal static string Title(Info e) { return e != null && !e.Auto ? BtcLang.Tr("Bear traps (own)") : BtcLang.Tr("Bear traps"); }

        private static void OnBound(Info e)
        {
            try
            {
                BtcTerraform.BeaconLook(e.Go, true, Icon, Title(e));
                Project pr = e.Go.GetComponent<Project>();
                if (pr != null) BtcGhTaskList.RefreshProject(pr);
            }
            catch (Exception ex) { BtcLog.Error("TrapTask", "OnBound: " + ex.Message); }
        }

        /// <summary>Own tasks of the local faction (sorted: auto first).</summary>
        internal static void OwnTasks(List<Info> outList)
        {
            outList.Clear();
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            if (mine == null) return;
            foreach (KeyValuePair<uint, Info> kv in ByNet) if (kv.Value.Go != null && kv.Value.Faction == mine.ID) outList.Add(kv.Value);
            outList.Sort((a, b) => a.Auto != b.Auto ? (a.Auto ? -1 : 1) : a.Nid.CompareTo(b.Nid));
        }

        /// <summary>The selected (vanilla task selection) trap task of the local faction, if any.</summary>
        internal static Info SelectedOwn()
        {
            if (ByNet.Count == 0) return null;
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            if (mine == null) return null;
            foreach (KeyValuePair<uint, Info> kv in ByNet)
            {
                if (kv.Value.Go == null || kv.Value.Faction != mine.ID) continue;
                Project p = kv.Value.Go.GetComponent<Project>();
                if (p != null && p.selection) return kv.Value;
            }
            return null;
        }

        internal static Info TaskOfTrap(uint trap)
        {
            foreach (KeyValuePair<uint, Info> kv in ByNet) if (kv.Value.Traps.Contains(trap)) return kv.Value;
            return null;
        }

        // ------------------------------------------------------------------ host AI
        private struct Reservation { internal Labor L; internal float At; }
        private static readonly Dictionary<uint, Reservation> Res = new Dictionary<uint, Reservation>();
        private struct StoreAt { internal bool Ok; internal float At; }
        private static readonly Dictionary<int, StoreAt> _storeCache = new Dictionary<int, StoreAt>();

        // a worker keeps his trap while he works for this task; the first seconds count before he is hired (the hire runs when the order starts)
        private static bool Holds(Reservation r, Project task, float now)
        {
            if (r.L == null || r.L.IsNullOrReleased()) return false;
            if (now - r.At > 90f) return false;
            return now - r.At < 4f || r.L.Project == task;
        }

        private static int FiredCount(Info e)
        {
            int n = 0;
            foreach (uint t in e.Traps) { Trap r; if (TryTrap(t, out r) && r.Fired) n++; }
            return n;
        }

        /// <summary>The nearest fired trap of the task that no other worker holds (reserve: hold it for this worker).</summary>
        private static bool FindTrap(Info e, Project task, Labor labor, bool reserve, out Trap best)
        {
            best = default(Trap);
            float bestD = float.MaxValue, now = Time.unscaledTime;
            Vector3 lp = labor.transform.position;
            bool found = false;
            foreach (uint t in e.Traps)
            {
                Trap r;
                if (!TryTrap(t, out r) || !r.Fired || r.A == null || r.A.Active == r.A.DesiredState) continue;
                Reservation h;
                if (Res.TryGetValue(t, out h) && h.L != labor && Holds(h, task, now)) continue;
                float d = (r.Pos - lp).sqrMagnitude;
                if (d < bestD) { bestD = d; best = r; found = true; }
            }
            if (found && reserve)
            {
                _tmp.Clear();
                foreach (KeyValuePair<uint, Reservation> kv in Res) if (kv.Value.L == labor) _tmp.Add(kv.Key);
                for (int i = 0; i < _tmp.Count; i++) Res.Remove(_tmp[i]);
                Reservation nr; nr.L = labor; nr.At = now;
                Res[best.Nid] = nr;
            }
            return found;
        }

        // something in the hands: only with a fitting container to put it in (cached 2 s per worker)
        private static bool HandsOk(Labor labor)
        {
            if (labor.recepteur == null || labor.recepteur.IsEmpty()) return true;
            float now = Time.unscaledTime;
            int id = labor.GetInstanceID();
            StoreAt s;
            if (_storeCache.TryGetValue(id, out s) && now - s.At < 2f) return s.Ok;
            s.Ok = labor.BestRecepteurToStoreInventory() != null;
            s.At = now;
            _storeCache[id] = s;
            return s.Ok;
        }

        internal static bool HasWorkFor(Info e, Project task, Labor labor)
        {
            if (labor == null || !task.HasRoomFor(labor)) return false;
            Trap t;
            return FindTrap(e, task, labor, false, out t) && HandsOk(labor);
        }

        /// <summary>Fired traps of the task (host: activator state; client: synced trap state; from the last 1 Hz scan).</summary>
        internal static int GoalCount(Info e) { return FiredCount(e); }

        private static readonly IconKey WorkIcon = IconKeys.Operator;

        internal static Node GetInstruction(Info e, Project task, Labor labor, Var<OrderPackage<Labor>> result)
        {
            Trap t;
            if (!HandsOk(labor) || !FindTrap(e, task, labor, true, out t)) return Node.Fail(FailException.Generic);
            float radius = Vector3.Distance(t.Pos, labor.transform.position) + 10f;
            if (labor.enemyTracker.EnemyInRadius(radius)) { Res.Remove(t.Nid); return Node.FailWork<Exceptions.CantWorkNearEnemy>(); }
            ObjectActivator act = t.A;
            WorkInstruction<Labor> wi = new WorkInstruction<Labor>("Re-arm trap", WorkIcon, l => Work(l, act));
            OrderPackage<Labor> pk = Order.Package.Of<Labor>(wi, Labor.DecisionSource.Project);
            pk.SetPrepare(delegate(BaseLabor bl) { return task.Hire(bl as Labor); });
            result.Set(pk);
            return Node.Empty;
        }

        // the worker first stores what he carries (no ingots / planks / kit parts dropped at the trap), then runs to the trap and re-arms it
        private static Node Work(Labor l, ObjectActivator act)
        {
            Node operate = (Node)OperateMi.Invoke(act, new object[] { l });
            if (l.recepteur == null || l.recepteur.IsEmpty()) return operate;
            return Node.Sequence.Do(l.GoStoreInBestStorage()).Do(() => operate);
        }

        // ------------------------------------------------------------------ save / load (host)
        private sealed class Pend { internal bool Auto; internal Vector3 Beacon; internal List<XYZ> Traps = new List<XYZ>(); }
        private static List<Pend> _pending;
        private static float _giveUpAt;

        internal static void OnSave(string folder)
        {
            if (!NetworkServer.active || !On || string.IsNullOrEmpty(folder)) return;
            string file = System.IO.Path.Combine(folder, FileName);
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (KeyValuePair<uint, Info> kv in ByNet)
            {
                Info e = kv.Value;
                if (!Alive(e)) continue;
                BeaconDriver d = e.Go.GetComponent<BeaconDriver>();
                if (d == null) continue;
                Vector3 b = d.NetworkNetworkPosition;
                sb.Append(e.Auto ? 'A' : 'C').Append(' ').Append(b.x.ToString("R", Inv)).Append(' ').Append(b.y.ToString("R", Inv)).Append(' ').Append(b.z.ToString("R", Inv));
                if (!e.Auto)
                    foreach (uint t in e.Traps)
                    {
                        Trap r; if (!TryTrap(t, out r)) continue;
                        XYZ p = r.T.voxel.position;
                        sb.Append(' ').Append(p.x).Append(',').Append(p.y).Append(',').Append(p.z);
                    }
                sb.Append('\n');
                n++;
            }
            if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
            File.WriteAllText(file, sb.ToString());
            BtcLog.Info("TrapTask", "saved " + n + " trap task(s)");
        }

        internal static void OnLoad(string folder)
        {
            ClearAll();
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
            string file = System.IO.Path.Combine(folder, FileName);
            if (!File.Exists(file)) return;
            List<Pend> list = new List<Pend>();
            foreach (string line in File.ReadAllLines(file))
            {
                string[] t = line.Split(' ');
                if (t.Length < 4 || (t[0] != "A" && t[0] != "C")) continue;
                float x, y, z;
                if (!float.TryParse(t[1], NumberStyles.Float, Inv, out x) || !float.TryParse(t[2], NumberStyles.Float, Inv, out y) || !float.TryParse(t[3], NumberStyles.Float, Inv, out z)) continue;
                Pend p = new Pend { Auto = t[0] == "A", Beacon = new Vector3(x, y, z) };
                for (int i = 4; i < t.Length; i++)
                {
                    string[] c = t[i].Split(',');
                    int a, b, d;
                    if (c.Length == 3 && int.TryParse(c[0], NumberStyles.Integer, Inv, out a) && int.TryParse(c[1], NumberStyles.Integer, Inv, out b) && int.TryParse(c[2], NumberStyles.Integer, Inv, out d))
                        p.Traps.Add(new XYZ(a, b, d));
                }
                list.Add(p);
            }
            if (list.Count == 0) return;
            _pending = list;
            _giveUpAt = Time.unscaledTime + 90f;
            BtcLog.Info("TrapTask", "save has " + list.Count + " trap task(s), matching after the load");
        }

        // after the load: the saved Repair tasks at the saved beacon positions become trap tasks again (before KeepAuto makes new ones)
        private static void ApplyPending(float now)
        {
            RepairProject[] all = UnityEngine.Object.FindObjectsOfType<RepairProject>();
            for (int i = 0; i < all.Length; i++)
            {
                RepairProject rp = all[i];
                if (rp == null || ByObj.ContainsKey(rp.gameObject.GetInstanceID()) || rp.faction == null) continue;
                NetworkIdentity ni = rp.GetComponent<NetworkIdentity>();
                BeaconDriver d = rp.GetComponent<BeaconDriver>();
                if (ni == null || ni.netId.Value == 0 || d == null) continue;
                for (int k = 0; k < _pending.Count; k++)
                {
                    Pend p = _pending[k];
                    if ((d.NetworkNetworkPosition - p.Beacon).sqrMagnitude > 0.25f) continue;
                    Info e = new Info { Go = rp.gameObject, Nid = ni.netId.Value, Auto = p.Auto, Faction = rp.faction.ID };
                    for (int j = 0; j < Traps.Count; j++)
                        if (Traps[j].Faction == e.Faction && p.Traps.Contains(Traps[j].T.voxel.position)) e.Traps.Add(Traps[j].Nid);
                    if (!e.Auto && e.Traps.Count == 0 && p.Traps.Count > 0) break;   // traps not loaded yet: try again next second
                    ByObj[rp.gameObject.GetInstanceID()] = e;
                    ByNet[e.Nid] = e;
                    rp.AutoDeleteDisabled = true;
                    OnBound(e);
                    _pending.RemoveAt(k);
                    _dirty = true;
                    BtcLog.Info("TrapTask", "restored " + (e.Auto ? "auto" : "own") + " task " + e.Nid + " (" + e.Traps.Count + " traps)");
                    break;
                }
            }
            if (_pending.Count == 0 || now > _giveUpAt)
            {
                if (_pending.Count > 0) BtcLog.Warn("TrapTask", _pending.Count + " saved trap task(s) not found after the load");
                _pending = null;
            }
        }
    }
}
