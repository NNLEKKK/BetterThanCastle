using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Legacy;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Wormhole wards (rule phantom.wardWorm, needs phantoms; user 2026-11-17, "variant of the sentinel ward" design, decisions 2026-11-17).
    /// A wormhole ward is a vanilla sentinel ward object with a mark: white instead of its colour, never shoots, and is a travel point of
    /// the phantom wormhole network (BtcPhantomWorm, together with the crystals). It is crafted at the altar (BtcWormWardCraft) and placed
    /// like any ward. Host-authoritative: the host keeps the marks (ward instance ids), the per-point "teleport off" switches (wards and
    /// crystals, pie menu) and sends them to clients (msg 119 "1|netId:off,...|nestNetId,..."); clients ask the host to switch a point
    /// (msg 120 "w|netId" / "n|netId", owner only). A point works only while it stands on the ground with no voxel in its cell and the
    /// cell above; with phantom.wardPower also only while it is powered (vanilla crystal/pylon link: WardController enabled).
    /// Marks survive saves in btc-wormwards.txt next to the save (matched by position). BC ON only; OFF = nothing of this exists.
    /// </summary>
    internal static class BtcWormWard
    {
        internal const short MsgState = 119, MsgToggle = 120;
        internal const string FileName = "btc-wormwards.txt";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class W
        {
            internal GameObject Go;
            internal uint NetId;
            internal bool Off;
        }

        private static readonly Dictionary<int, W> Marks = new Dictionary<int, W>();     // ward instance id -> mark (host: truth, client: mirror)
        private static readonly HashSet<int> NestsOff = new HashSet<int>();              // crystal (FireflyNest) instance ids with teleport off
        private static readonly List<GameObject> Live = new List<GameObject>();
        private static readonly List<int> Tmp = new List<int>();
        private static int iOn = -1, iPower = -1;
        private static bool _dirty, _wasOn;
        private static float _nextStep, _nextSend, _applyUntil;
        private static string _lastState = string.Empty;
        private static List<Vector4> _pending;      // loaded ward marks: x y z off
        private static List<Vector3> _pendingNests; // loaded crystals with teleport off

        /// <summary>Host: wormhole wards act as travel points this frame (phantoms on, rule on, in a match).</summary>
        internal static bool Active;

        internal static void RegisterRules(string g)
        {
            BtcGameplay.Add("phantom.wardWorm", g, "Wormhole wards", "Варды-червоточины", 0f, 0f, 1f, 1f, true,
                "A new ward crafted at the altar (1 iron ingot, 30 sulfur): a white sentinel ward that never shoots. Placed anywhere, all own wormhole wards and crystals are instant travel points for phantoms (crystals only with Crystal wormholes on). Normal bricktrons ignore them. A ward with a voxel in it or right above it does not work. Its teleport (and the one of a crystal) can be switched off from the pie menu.",
                "Новый вард, крафтится в алтаре (1 железный слиток, 30 серы): белый вард часового, который не стреляет. Ставится в любое место; все свои варды-червоточины и кристаллы становятся для фантомов точками мгновенного перемещения (кристаллы, только если включены кристальные порталы). Обычные бриктроны их не используют. Вард, в котором или прямо над которым есть воксель, не работает. Телепорт варда (и кристалла) можно выключить через круговое меню.");
            BtcGameplay.Add("phantom.wardPower", g, "Wormhole wards: only when powered", "Варды-червоточины: только с запиткой", 0f, 0f, 1f, 1f, true,
                "A wormhole ward works only while it is powered like vanilla wards (near a crystal or linked through pylons). Off: it works wherever it stands.",
                "Вард-червоточина работает, только пока запитан как обычные варды (рядом с кристаллом или через пилоны). Выкл: работает где угодно.");
            iOn = BtcGameplay.Idx("phantom.wardWorm"); iPower = BtcGameplay.Idx("phantom.wardPower");
        }

        private static float Val(int i)
        {
            float[] e = BtcGameplay.E;
            return (e == null || i < 0 || i >= e.Length) ? 0f : e[i];
        }

        /// <summary>The rule is on (phantoms on too, BC ON): the item can be crafted and placed wards are marked.</summary>
        internal static bool RuleOn { get { return BtcPhantom.RuleOn && Val(iOn) > 0.5f; } }
        internal static bool PowerOnly { get { return Val(iPower) > 0.5f; } }

        private static bool Host { get { return NetworkServer.active && BtcReplay.Mode != BtcReplay.State.Playing; } }

        // ------------------------------------------------------------------ queries
        internal static bool IsWorm(GameObject go)
        {
            return go != null && Marks.Count > 0 && Marks.ContainsKey(go.GetInstanceID());
        }

        internal static bool IsOff(GameObject go)
        {
            W w;
            return go != null && Marks.TryGetValue(go.GetInstanceID(), out w) && w.Off;
        }

        internal static bool NestOff(FireflyNest n) { return n != null && NestsOff.Count > 0 && NestsOff.Contains(n.gameObject.GetInstanceID()); }

        /// <summary>Host: the marked wards that exist (not switched off; Usable checks the rest).</summary>
        internal static List<GameObject> Wards()
        {
            Live.Clear();
            foreach (W w in Marks.Values) if (w.Go != null && !w.Off) Live.Add(w.Go);
            return Live;
        }

        /// <summary>Stands on the ground (not carried, not stored), switched on, powered if the rule asks, no voxel in its cell or above.</summary>
        internal static bool Usable(GameObject go)
        {
            if (go == null || !go.activeInHierarchy || IsOff(go)) return false;
            if (!Placed(go)) return false;
            if (PowerOnly)
            {
                WardController wc = go.GetComponent<WardController>();
                if (wc == null || !wc.enabled) return false;
            }
            return Clear(go.transform.position);
        }

        internal static bool Placed(GameObject go)
        {
            Transform p = go.transform.parent;
            return p == null || p.GetComponentInParent<Labor>() == null;
        }

        /// <summary>No voxel in the ward and right above it (user: a buried ward must not teleport). The ward stands in the cell of its
        /// foot (XYZ.FromVector3 rounds, so on smooth ground that cell is the surface crust: allowed), its body is the next cell up and
        /// the cell above the body is "1 block above" (a block placed on top of the ward lands there).</summary>
        internal static bool Clear(Vector3 pos)
        {
            try
            {
                Brix.Engine.XYZ c = Brix.Engine.XYZ.FromVector3(pos + Vector3.up * 0.1f);
                if (!Brix.Engine.Voxel.IsEmpty(c) && !Brix.Engine.Voxel.IsCrust(c)) return false;
                return Brix.Engine.Voxel.IsEmpty(new Brix.Engine.XYZ(c.x, c.y + 1, c.z)) && Brix.Engine.Voxel.IsEmpty(new Brix.Engine.XYZ(c.x, c.y + 2, c.z));
            }
            catch (Exception e) { BtcLog.Warn("WormWard", "voxel check: " + e.Message); return false; }
        }

        // ------------------------------------------------------------------ host changes
        /// <summary>Host: mark a sentinel ward object as a wormhole ward (crafted, loaded, DEV).</summary>
        internal static void Mark(GameObject go, bool off)
        {
            if (go == null || !Host) return;
            int id = go.GetInstanceID();
            W w;
            if (!Marks.TryGetValue(id, out w)) { w = new W { Go = go }; Marks[id] = w; }
            w.Off = off;
            NetworkIdentity ni = go.GetComponent<NetworkIdentity>();
            w.NetId = ni != null ? ni.netId.Value : 0u;
            BtcWormWardLook.Apply(go, off);
            _dirty = true;
            BtcLog.Info("WormWard", "mark " + go.name + " id=" + id + " net=" + w.NetId + " at=" + go.transform.position);
        }

        /// <summary>The object went back to the pool (WardController.ResetComponent): it is a plain sentinel ward again.</summary>
        internal static void Forget(GameObject go)
        {
            if (go == null || Marks.Count == 0) return;
            if (Marks.Remove(go.GetInstanceID()))
            {
                BtcLog.Info("WormWard", "forget " + go.name + " (back to the pool) at=" + go.transform.position);
#if DEV
                {
                    System.Diagnostics.StackFrame[] fr = new System.Diagnostics.StackTrace(1, false).GetFrames();
                    System.Text.StringBuilder st = new System.Text.StringBuilder();
                    for (int i = 0; fr != null && i < fr.Length && i < 40; i++) { System.Reflection.MethodBase mb = fr[i].GetMethod(); if (mb == null || mb.DeclaringType == null) continue; string tn = mb.DeclaringType.Name; if (tn.IndexOf("Slinq", StringComparison.Ordinal) >= 0 || tn.IndexOf("<>", StringComparison.Ordinal) >= 0) continue; st.Append(tn).Append('.').Append(mb.Name).Append(" < "); }
                    BtcLog.Info("WormTrace", "forget by " + st);
                }
#endif
                BtcWormWardLook.Restore(go);
                _dirty = true;
            }
        }

        private static void SetOff(GameObject go, bool off)
        {
            W w;
            if (go == null || !Marks.TryGetValue(go.GetInstanceID(), out w) || w.Off == off) return;
            w.Off = off;
            BtcWormWardLook.Apply(go, off);
            _dirty = true;
        }

        private static void SetNestOff(FireflyNest n, bool off)
        {
            if (n == null) return;
            int id = n.gameObject.GetInstanceID();
            if (off ? NestsOff.Add(id) : NestsOff.Remove(id)) _dirty = true;
        }

        // ------------------------------------------------------------------ pie menu (Info/Livre text injected by Patches/LivrePatches.cs)
        private static GameObject Target() { return Instructions.objet as GameObject; }

        private static bool Own(GameObject go)
        {
            User u = User.LocalUser;
            Faction f = Faction.GetFaction(go);
            return u != null && f != null && ReferenceEquals(u.faction, f);
        }

        private static FireflyNest TargetNest()
        {
            GameObject go = Target();
            return go != null ? go.GetComponentInParent<FireflyNest>() : null;
        }

        internal static bool PieWard(bool wantOff)
        {
            GameObject go = Target();
            return RuleOn && IsWorm(go) && Own(go) && IsOff(go) == wantOff;
        }

        internal static bool PieNest(bool wantOff)
        {
            FireflyNest n = TargetNest();
            return RuleOn && BtcPhantom.Worm && n != null && Own(n.gameObject) && NestOff(n) == wantOff;
        }

        internal static void PieToggleWard()
        {
            GameObject go = Target();
            if (!IsWorm(go)) return;
            if (Host) SetOff(go, !IsOff(go));
            else Ask("w", go);
        }

        internal static void PieToggleNest()
        {
            FireflyNest n = TargetNest();
            if (n == null) return;
            if (Host) SetNestOff(n, !NestOff(n));
            else Ask("n", n.gameObject);
        }

        private static void Ask(string kind, GameObject go)
        {
            try
            {
                NetworkIdentity ni = go.GetComponent<NetworkIdentity>();
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (ni == null || m == null || m.LocalNetworkClient == null || !BtcNetworkGate.Enabled) return;
                m.LocalNetworkClient.Send(MsgToggle, new StringMessage(kind + "|" + ni.netId.Value.ToString(Inv)));
            }
            catch (Exception e) { BtcLog.Error("WormWard", "Ask: " + e); }
        }

        internal static void Install()
        {
            Instructions.conditions["Condition_Worm_Ward_On"] = () => PieWard(false);
            Instructions.conditions["Condition_Worm_Ward_Off"] = () => PieWard(true);
            Instructions.conditions["Condition_Worm_Nest_On"] = () => PieNest(false);
            Instructions.conditions["Condition_Worm_Nest_Off"] = () => PieNest(true);
            Instructions.commendes["Event_Worm_Ward_Toggle"] = () => PieToggleWard();
            Instructions.commendes["Event_Worm_Nest_Toggle"] = () => PieToggleNest();
        }

        // ------------------------------------------------------------------ tick (from BtcPhantom.Tick, every frame)
        internal static void Tick(bool phantomsOn)
        {
            try
            {
                bool on = phantomsOn && RuleOn;
                Active = on && Host;
                if (!on)
                {
                    if (_wasOn) Clear();
                    return;
                }
                _wasOn = true;
                float un = Time.unscaledTime;
                if (un < _nextStep) return;
                _nextStep = un + 0.5f;
                if (Host)
                {
                    Prune();
                    if (_pending != null || _pendingNests != null) ApplyPending(un);
                    if (BtcWormWardBuild.PendingLoad != null) BtcWormWardBuild.ApplyLoad();
                    if (_dirty || un >= _nextSend) { _dirty = false; _nextSend = un + 2f; Broadcast(); }
                }
                else ClientApply();
                TickHeld();
            }
            catch (Exception e) { BtcLog.Error("WormWard", "tick: " + e); }
        }

        // ------------------------------------------------------------------ carried look: the held prop (HeldSentinelWard accessory) of a worker
        // that carries a wormhole ward gets the white gem too; restored when it carries something else
        private static readonly Dictionary<int, GameObject> HeldProps = new Dictionary<int, GameObject>();   // prop renderer GO id -> GO
        private static readonly HashSet<int> HeldSeen = new HashSet<int>();
        private static readonly List<int> HeldTmp = new List<int>();

        private static void TickHeld()
        {
            HeldSeen.Clear();
            foreach (W w in Marks.Values)
            {
                if (w.Go == null || w.Go.activeInHierarchy) continue;            // standing wards are active; a carried one is not
                Transform par = w.Go.transform.parent;
                Labor l = par != null ? par.GetComponentInParent<Labor>() : null;
                if (l == null) continue;
                foreach (Renderer r in l.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null || r is ParticleSystemRenderer || !BtcWormWardLook.IsGemOf(r)) continue;
                    GameObject g = r.gameObject;
                    int id = g.GetInstanceID();
                    HeldSeen.Add(id);
                    if (!HeldProps.ContainsKey(id)) { HeldProps[id] = g; BtcWormWardLook.Apply(g, w.Off); BtcLog.Info("WormWard", "held prop " + g.name + " on " + l.name + " whitened"); }
                }
            }
            if (HeldProps.Count == HeldSeen.Count) return;
            HeldTmp.Clear();
            foreach (KeyValuePair<int, GameObject> kv in HeldProps) if (!HeldSeen.Contains(kv.Key)) HeldTmp.Add(kv.Key);
            for (int i = 0; i < HeldTmp.Count; i++)
            {
                GameObject g = HeldProps[HeldTmp[i]];
                if (g != null) BtcWormWardLook.Restore(g);
                HeldProps.Remove(HeldTmp[i]);
            }
        }

        private static void Prune()
        {
            Tmp.Clear();
            foreach (KeyValuePair<int, W> kv in Marks) if (kv.Value.Go == null) Tmp.Add(kv.Key);
            for (int i = 0; i < Tmp.Count; i++) Marks.Remove(Tmp[i]);
            if (Tmp.Count > 0) _dirty = true;
            foreach (W w in Marks.Values)
                if (w.NetId == 0u)
                {
                    NetworkIdentity ni = w.Go.GetComponent<NetworkIdentity>();
                    if (ni != null && ni.netId.Value != 0u) { w.NetId = ni.netId.Value; _dirty = true; }
                }
        }

        private static void Clear()
        {
            _wasOn = false;
            Active = false;
            foreach (W w in Marks.Values) if (w.Go != null) BtcWormWardLook.Restore(w.Go);
            Marks.Clear();
            NestsOff.Clear();
            _lastState = string.Empty;
            BtcWormWardBuild.Clear();
            BtcLog.Info("WormWard", "cleared");
        }

        // ------------------------------------------------------------------ wire
        private static string State()
        {
            StringBuilder sb = new StringBuilder("1|");
            bool first = true;
            foreach (W w in Marks.Values)
            {
                if (w.Go == null || w.NetId == 0u) continue;
                if (!first) sb.Append(',');
                sb.Append(w.NetId.ToString(Inv)).Append(':').Append(w.Off ? '1' : '0');
                first = false;
            }
            sb.Append('|');
            first = true;
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests != null)
                for (int i = 0; i < nests.Count; i++)
                {
                    FireflyNest n = nests[i];
                    if (n == null || !NestsOff.Contains(n.gameObject.GetInstanceID())) continue;
                    NetworkIdentity ni = n.GetComponent<NetworkIdentity>();
                    if (ni == null) continue;
                    if (!first) sb.Append(',');
                    sb.Append(ni.netId.Value.ToString(Inv));
                    first = false;
                }
            return sb.ToString();
        }

        private static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringMessage msg = new StringMessage(State());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("WormWard", "Broadcast: " + e); }
        }

        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                if (NetworkServer.active && clientOverride == null)
                {
                    NetworkServer.UnregisterHandler(MsgToggle);
                    NetworkServer.RegisterHandler(MsgToggle, OnToggleAtHost);
                }
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnState);
                }
            }
            catch (Exception e) { BtcLog.Error("WormWard", "RegisterHandlers: " + e); }
        }

        private static void OnToggleAtHost(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (!Host || !RuleOn || msg == null || msg.value == null) return;
                string[] a = msg.value.Split('|');
                if (a.Length > 0 && a[0] == "b")
                {
                    User bu = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                    if (bu != null) BtcWormWardBuild.OnTagMsg(a, bu.faction);
                    return;
                }
                uint id;
                if (a.Length < 2 || !uint.TryParse(a[1], NumberStyles.Integer, Inv, out id)) return;
                GameObject go = NetworkServer.FindLocalObject(new NetworkInstanceId(id));
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                if (go == null || u == null || !ReferenceEquals(u.faction, Faction.GetFaction(go))) return;   // owner only
                if (a[0] == "w") SetOff(go, !IsOff(go));
                else if (a[0] == "n")
                {
                    FireflyNest n = go.GetComponent<FireflyNest>();
                    if (n != null) SetNestOff(n, !NestOff(n));
                }
            }
            catch (Exception e) { BtcLog.Error("WormWard", "OnToggleAtHost: " + e); }
        }

        private static void OnState(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (Host || !BtcNetworkGate.Enabled || msg == null || msg.value == null) return;
                _lastState = msg.value;
                ClientApply();
            }
            catch (Exception e) { BtcLog.Error("WormWard", "OnState: " + e); }
        }

        /// <summary>Client (and replay viewer): mirror the host marks and the crystal switches from the last state.</summary>
        private static void ClientApply()
        {
            string[] parts = _lastState.Split('|');
            if (parts.Length < 3 || parts[0] != "1") return;
            HashSet<int> seen = new HashSet<int>();
            if (parts[1].Length > 0)
                foreach (string s in parts[1].Split(','))
                {
                    int c = s.IndexOf(':');
                    uint id;
                    if (c < 0 || !uint.TryParse(s.Substring(0, c), NumberStyles.Integer, Inv, out id)) continue;
                    GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
                    if (go == null) continue;
                    bool off = s.Substring(c + 1) == "1";
                    int iid = go.GetInstanceID();
                    seen.Add(iid);
                    W w;
                    if (!Marks.TryGetValue(iid, out w)) { w = new W { Go = go, NetId = id, Off = !off }; Marks[iid] = w; }
                    if (w.Off != off || !BtcWormWardLook.Has(go)) { w.Off = off; BtcWormWardLook.Apply(go, off); }
                }
            Tmp.Clear();
            foreach (KeyValuePair<int, W> kv in Marks) if (!seen.Contains(kv.Key)) Tmp.Add(kv.Key);
            for (int i = 0; i < Tmp.Count; i++)
            {
                W w = Marks[Tmp[i]];
                if (w.Go != null) BtcWormWardLook.Restore(w.Go);
                Marks.Remove(Tmp[i]);
            }
            NestsOff.Clear();
            if (parts[2].Length > 0)
                foreach (string s in parts[2].Split(','))
                {
                    uint id;
                    if (!uint.TryParse(s, NumberStyles.Integer, Inv, out id)) continue;
                    GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
                    if (go != null) NestsOff.Add(go.GetInstanceID());
                }
        }

        // ------------------------------------------------------------------ save sidecar (Patches/InfHealPatches.cs)
        internal static void OnSave(string folder)
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
            try
            {
                string file = System.IO.Path.Combine(folder, FileName);
                StringBuilder sb = new StringBuilder();
                int n = 0;
                foreach (W w in Marks.Values)
                {
                    if (w.Go == null) continue;
                    Labor carrier = w.Go.transform.parent != null ? w.Go.transform.parent.GetComponentInParent<Labor>() : null;     // carried: BtcHeldWards gives a fresh one to the worker nearest to this point
                    if (carrier == null)
                        foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                        {
                            GameObject held;
                            if (l.recepteur != null && l.recepteur.FirstObjectOf(Brix.External.Factories.ObjetsDynamiques.SentinelWard, out held) && held == w.Go) { carrier = l; break; }
                        }
                    Vector3 p = carrier != null ? carrier.transform.position : w.Go.transform.position;
                    sb.Append("w ").Append(F(p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append(' ').Append(w.Off ? '1' : '0').Append('\n');
                    n++;
                }
                List<FireflyNest> nests = BtcMatchStats.NestList();
                if (nests != null)
                    for (int i = 0; i < nests.Count; i++)
                    {
                        FireflyNest nest = nests[i];
                        if (nest == null || !NestsOff.Contains(nest.gameObject.GetInstanceID())) continue;
                        Vector3 p = nest.transform.position;
                        sb.Append("n ").Append(F(p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
                        n++;
                    }
                BtcWormWardBuild.AppendSave(sb, ref n);
                if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
                File.WriteAllText(file, sb.ToString());
                BtcLog.Info("WormWard", "saved " + n + " line(s)");
            }
            catch (Exception e) { BtcLog.Error("WormWard", "OnSave: " + e); }
        }

        internal static void OnLoad(string folder)
        {
            _pending = null; _pendingNests = null;
            BtcWormWardBuild.PendingLoad = null;
            Marks.Clear(); NestsOff.Clear();
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || string.IsNullOrEmpty(folder)) return;
            try
            {
                string file = System.IO.Path.Combine(folder, FileName);
                if (!File.Exists(file)) return;
                List<Vector4> wl = new List<Vector4>();
                List<Vector3> nl = new List<Vector3>();
                List<Vector3> bl = new List<Vector3>();
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] a = line.Trim().Split(' ');
                    if (a.Length < 4) continue;
                    float x, y, z;
                    if (!float.TryParse(a[1], NumberStyles.Float, Inv, out x) || !float.TryParse(a[2], NumberStyles.Float, Inv, out y)
                        || !float.TryParse(a[3], NumberStyles.Float, Inv, out z)) continue;
                    if (a[0] == "w") wl.Add(new Vector4(x, y, z, a.Length > 4 && a[4] == "1" ? 1f : 0f));
                    else if (a[0] == "n") nl.Add(new Vector3(x, y, z));
                    else if (a[0] == "b") bl.Add(new Vector3(x, y, z));
                }
                _pending = wl.Count > 0 ? wl : null;
                BtcWormWardBuild.PendingLoad = bl.Count > 0 ? bl : null;
                _pendingNests = nl.Count > 0 ? nl : null;
                _applyUntil = -1f;
                BtcLog.Info("WormWard", "load: " + wl.Count + " ward(s), " + nl.Count + " crystal switch(es) pending");
            }
            catch (Exception e) { BtcLog.Error("WormWard", "OnLoad: " + e); }
        }

        /// <summary>BtcHeldWards gave a fresh ward to a worker for a carried ward saved at <paramref name="spot"/> (the carrier position,
        /// written by both files the same way): a wormhole ward saved there gets its mark back on the new object.</summary>
        internal static void HeldGiven(Vector3 spot, GameObject ward)
        {
            if (_pending == null || ward == null || ward.GetComponent<Brix.Game.Components.Wards.WardAttack>() == null) return;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (((Vector3)_pending[i] - spot).sqrMagnitude > 0.01f) continue;
                Mark(ward, _pending[i].w > 0.5f);
                _pending.RemoveAt(i);
                if (_pending.Count == 0) _pending = null;
                return;
            }
        }

        /// <summary>Host, after the load: mark the nearest sentinel ward (WardAttack) within 1 block of every saved one; retries for 30 s.</summary>
        private static void ApplyPending(float un)
        {
            if (_applyUntil < 0f) _applyUntil = un + 30f;
            if (_pending != null)
            {
                List<GameObject> all = new List<GameObject>();       // standing sentinel wards + the ones workers hold (inactive)
                List<Vector3> at = new List<Vector3>();
                foreach (Brix.Game.Components.Wards.WardAttack a in UnityEngine.Object.FindObjectsOfType<Brix.Game.Components.Wards.WardAttack>())
                {
                    all.Add(a.gameObject);
                    at.Add(a.transform.position);
                }
                foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                {
                    GameObject held;
                    if (l.recepteur != null && l.recepteur.FirstObjectOf(Brix.External.Factories.ObjetsDynamiques.SentinelWard, out held) && held != null)
                    {
                        all.Add(held);
                        at.Add(l.transform.position);
                    }
                }
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    Vector3 p = _pending[i];
                    GameObject best = null;
                    float bd = 1f;
                    for (int k = 0; k < all.Count; k++)
                    {
                        GameObject go = all[k];
                        if (IsWorm(go)) continue;
                        float d = (at[k] - p).sqrMagnitude;
                        if (d < bd) { bd = d; best = go; }
                    }
                    if (best == null) continue;
                    Mark(best, _pending[i].w > 0.5f);
                    _pending.RemoveAt(i);
                }
                if (_pending.Count == 0) _pending = null;
            }
            if (_pendingNests != null)
            {
                List<FireflyNest> nests = BtcMatchStats.NestList();
                for (int i = _pendingNests.Count - 1; nests != null && i >= 0; i--)
                    for (int k = 0; k < nests.Count; k++)
                        if (nests[k] != null && (nests[k].transform.position - _pendingNests[i]).sqrMagnitude < 1f)
                        {
                            SetNestOff(nests[k], true);
                            _pendingNests.RemoveAt(i);
                            break;
                        }
                if (_pendingNests.Count == 0) _pendingNests = null;
            }
            if (un > _applyUntil && (_pending != null || _pendingNests != null))
            {
                BtcLog.Warn("WormWard", "load: " + (_pending != null ? _pending.Count : 0) + " ward(s) / " + (_pendingNests != null ? _pendingNests.Count : 0) + " crystal(s) not found");
                _pending = null; _pendingNests = null;
            }
        }

        private static string F(float v) { return v.ToString("F2", Inv); }

        // ------------------------------------------------------------------ DEV/lab helpers
        internal static int Count { get { return Marks.Count; } }
        internal static List<GameObject> AllMarked() { List<GameObject> l = new List<GameObject>(); foreach (W w in Marks.Values) if (w.Go != null) l.Add(w.Go); return l; }
        internal static string Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("active=").Append(Active).Append(" rule=").Append(RuleOn).Append(" power=").Append(PowerOnly).Append(" marks=").Append(Marks.Count)
              .Append(" nestsOff=").Append(NestsOff.Count);
            foreach (W w in Marks.Values)
            {
                if (w.Go == null) { sb.Append(" [gone]"); continue; }
                WardController wc = w.Go.GetComponent<WardController>();
                Transform par = w.Go.transform.parent;
                sb.Append(" [").Append(w.Go.name).Append(" net=").Append(w.NetId).Append(" off=").Append(w.Off).Append(" white=").Append(BtcWormWardLook.Has(w.Go)).Append(" own=").Append(Own(w.Go)).Append(" act=").Append(w.Go.activeInHierarchy)
                  .Append(" placed=").Append(Placed(w.Go)).Append(" parent=").Append(par != null ? par.name : "-").Append(" powered=").Append(wc != null && wc.enabled)
                  .Append(" clear=").Append(Clear(w.Go.transform.position)).Append(" usable=").Append(Usable(w.Go)).Append(" at=").Append(w.Go.transform.position);
                Brix.Engine.XYZ c = Brix.Engine.XYZ.FromVector3(w.Go.transform.position + Vector3.up * 0.1f);
                sb.Append(" cell=").Append(c).Append(" col(-1..+2)=");
                for (int dy = -1; dy <= 2; dy++)
                    sb.Append(Brix.Engine.Voxel.Engine.IsVoxelNonEmpty(Brix.Engine.Voxel.Engine.GetVoxelHandle(new Brix.Engine.XYZ(c.x, c.y + dy, c.z))) ? '#' : '.');
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
