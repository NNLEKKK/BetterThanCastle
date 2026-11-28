using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Semantique;
using Brix.Legacy;
using Brix.UI.Icons;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Per-kit filter on tool racks (BC ON only). One kit per rack or Any (-1 = vanilla behaviour). Kits already on the rack stay until taken.
    /// State: rack key (netId, or -instanceID without identity) -> kit index; the vanilla exclusion machinery (Recepteur.MakeExclusive, which already
    /// syncs through CmdAddExclusion/RpcAddExclusion) does the actual storage restriction once ToolRack.RefreshExclusivity returns the chosen kit list.
    /// Wire (mod ids 85 request client->host, 86 full state host->clients): Net/BtcRackFilterNet.cs.
    /// </summary>
    internal static class BtcRackFilter
    {
        internal const int Any = -1;
        /// <summary>Index order is part of the wire format.</summary>
        internal static readonly string[] KitNames = { "Knight", "Archer", "Halberdier", "Arbalist", "Alchemist", "Artificer" };
        private static readonly string[] KitFields = { "Knight", "Archer", "Halbardier", "Arbalist", "Alchemist", "Artificer" };
        /// <summary>Vanilla icon names (PieMenu_Buttons.txt) per kit.</summary>
        internal static readonly string[] KitIcons = { "_Knight_Kit", "_Archer_Kit", "_Halberdier_Kit", "_Arbalist_Kit", "_Alchemist_Kit", "_Artificer_Kit" };

        internal static readonly Dictionary<long, int> Map = new Dictionary<long, int>();
        private static List<Ressource>[] _lists;
        private static FieldInfo _fExcl;
        private const float DeriveWindow = 20f;

        internal static bool Enabled { get { return BtcNetworkGate.Enabled; } }

        internal static void Clear() { Map.Clear(); }

        internal static long KeyOf(ToolRack r)
        {
            NetworkIdentity ni = r.GetComponent<NetworkIdentity>();
            if (ni != null && ni.netId.Value != 0u) return (long)ni.netId.Value;
            return -(long)r.gameObject.GetInstanceID();
        }

        internal static int Get(ToolRack r)
        {
            int k;
            return r != null && Map.TryGetValue(KeyOf(r), out k) ? k : Any;
        }

        internal static List<Ressource> ListFor(int kit)
        {
            if (kit < 0 || kit >= KitNames.Length) return null;
            if (_lists == null)
            {
                List<Ressource>[] l = new List<Ressource>[KitNames.Length];
                for (int i = 0; i < l.Length; i++)
                {
                    FieldInfo f = AccessTools.Field(typeof(ToolRack), KitFields[i]);
                    l[i] = f != null ? f.GetValue(null) as List<Ressource> : null;
                }
                _lists = l;
            }
            return _lists[kit];
        }

        internal static ToolRack TargetRack()
        {
            GameObject go = Instructions.objet as GameObject;
            return go != null ? go.GetComponent<ToolRack>() : null;
        }

        /// <summary>Local apply on this peer (no network): set the map entry and refresh the rack.</summary>
        internal static void ApplyLocal(ToolRack rack, int kit)
        {
            if (rack == null) return;
            long key = KeyOf(rack);
            if (kit < 0) Map.Remove(key); else Map[key] = kit;
            Refresh(rack);
        }

        internal static void Refresh(ToolRack rack)
        {
            try
            {
                if (rack == null || rack.recepteur == null) return;
                rack.RefreshExclusivity();
                RecepteurOverheadDisplayDriver d = rack.GetComponentInChildren<RecepteurOverheadDisplayDriver>(true);
                if (d != null)
                {
                    if (_mBig == null) _mBig = AccessTools.Method(typeof(RecepteurOverheadDisplayDriver), "SetBigIcon");
                    if (_mBig != null && d.overheadDisplay != null && d.overheadDisplay.BigIcon.IsSpawned) _mBig.Invoke(d, null);
                }
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "Refresh: " + e); }
        }

        private static MethodInfo _mBig;
        private static readonly Sprite[] Sprites = new Sprite[6];

        /// <summary>Kit icon (vanilla pie icon) for the Alt overhead display; null if the icon db has no entry.</summary>
        internal static Sprite KitSprite(int kit)
        {
            if (kit < 0 || kit >= Sprites.Length) return null;
            if (Sprites[kit] == null)
            {
                MetaIcon mi = IconDB._Unsafe_GetIcon_NEVERUSE(null, KitIcons[kit]);
                if (mi != null) Sprites[kit] = mi.Image64 != null ? mi.Image64 : mi.Image32;
            }
            return Sprites[kit];
        }

        internal static bool IsOwn(ToolRack rack)
        {
            User u = User.LocalUser;
            return rack != null && u != null && u.faction != null && rack.faction == u.faction;
        }

        /// <summary>UI entry (pie button): host/offline applies and broadcasts, a client asks the host.</summary>
        internal static void Request(ToolRack rack, int kit)
        {
            if (!Enabled || rack == null) return;
            try
            {
                if (!IsOwn(rack)) return;
                if (NetworkClient.active && !NetworkServer.active) BtcRackFilterNet.SendRequest(rack, kit);
                else
                {
                    ApplyLocal(rack, kit);
                    BtcRackFilterNet.Broadcast();
                }
                BtcLog.Info("RackFilter", "set " + (kit < 0 ? "Any" : KitNames[kit]) + " on rack " + KeyOf(rack));
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "Request: " + e); }
        }

        // ------------------------------------------------------------------ ToolRack hooks
        /// <summary>Prefix of ToolRack.RefreshExclusivity: false = skip vanilla (a filter is active and handled here).</summary>
        internal static bool RefreshPrefix(ToolRack rack)
        {
            if (!Enabled) return true;
            int kit = Get(rack);
            if (kit < 0) return true;
            List<Ressource> list = ListFor(kit);
            if (list == null) return true;
            if (_fExcl == null) _fExcl = AccessTools.Field(typeof(ToolRack), "_exclusivity");
            List<Ressource> cur = _fExcl.GetValue(rack) as List<Ressource>;
            if (!ReferenceEquals(list, cur))
            {
                rack.recepteur.MakeExclusive(list);
                _fExcl.SetValue(rack, list);
            }
            return false;
        }

        internal static void OnRackStart(ToolRack rack)
        {
            if (!Enabled || rack == null || rack.recepteur == null) return;
            if (Get(rack) >= 0) { Refresh(rack); return; }
            float t0 = Time.unscaledTime;
            // save restore: an empty rack whose restored exclusions leave exactly one kit open gets that kit as its filter
            rack.recepteur.ExclusionChanged.Connect(delegate(Ressource r) { TryDerive(rack, t0); });
        }

        private static void TryDerive(ToolRack rack, float t0)
        {
            try
            {
                if (!Enabled || rack == null || Time.unscaledTime - t0 > DeriveWindow || Get(rack) >= 0) return;
                Recepteur rc = rack.recepteur;
                GameObject first;
                rc.FirstObject(out first);
                if (first != null) return;
                int open = -1;
                for (int k = 0; k < KitNames.Length; k++)
                {
                    List<Ressource> l = ListFor(k);
                    if (l == null) return;
                    int excluded = 0;
                    for (int i = 0; i < l.Count; i++) if (rc.IsExcluding(l[i])) excluded++;
                    if (excluded == 0) { if (open >= 0) return; open = k; }
                    else if (excluded != l.Count) return;
                }
                if (open < 0) return;
                BtcLog.Info("RackFilter", "derived " + KitNames[open] + " from restored exclusions on rack " + KeyOf(rack));
                ApplyLocal(rack, open);
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "TryDerive: " + e); }
        }

        // ------------------------------------------------------------------ saves (host): btc-rackfilter.txt "x y z kit" in the save folder
        // The vanilla save keeps only the rack exclusions; TryDerive above can rebuild a filter from them on EMPTY racks only, so racks with kits
        // on them lost their filter. Restored by position (nearest rack within 1.5 blocks), retry for 30 s.
        internal const string FileName = "btc-rackfilter.txt";
        private static List<Vector4> _pending;
        private static float _applyAt, _deadline;

        internal static void OnSave(string folder)
        {
            if (!NetworkServer.active || !Enabled || string.IsNullOrEmpty(folder)) return;
            try
            {
                string file = System.IO.Path.Combine(folder, FileName);
                System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int n = 0;
                ToolRack[] all = BtcRegistry.RacksArray();
                for (int i = 0; i < all.Length; i++)
                {
                    int kit = Get(all[i]);
                    if (kit < 0) continue;
                    Vector3 p = all[i].transform.position;
                    sb.Append(p.x.ToString("F2", c)).Append(' ').Append(p.y.ToString("F2", c)).Append(' ').Append(p.z.ToString("F2", c)).Append(' ').Append(kit).Append('\n');
                    n++;
                }
                if (n == 0) { if (System.IO.File.Exists(file)) System.IO.File.Delete(file); return; }
                System.IO.File.WriteAllText(file, sb.ToString());
                BtcLog.Info("RackFilter", "saved " + n + " rack filter(s)");
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "OnSave: " + e); }
        }

        internal static void OnLoad(string folder)
        {
            _pending = null;
            if (!NetworkServer.active || !Enabled || string.IsNullOrEmpty(folder)) return;
            try
            {
                string file = System.IO.Path.Combine(folder, FileName);
                if (!System.IO.File.Exists(file)) return;
                System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
                List<Vector4> list = new List<Vector4>();
                string[] lines = System.IO.File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] t = lines[i].Split(' ');
                    float x, y, z; int k;
                    if (t.Length == 4 && float.TryParse(t[0], System.Globalization.NumberStyles.Float, c, out x) && float.TryParse(t[1], System.Globalization.NumberStyles.Float, c, out y)
                        && float.TryParse(t[2], System.Globalization.NumberStyles.Float, c, out z) && int.TryParse(t[3], out k) && k >= 0 && k < KitNames.Length)
                        list.Add(new Vector4(x, y, z, k));
                }
                if (list.Count == 0) return;
                _pending = list;
                _applyAt = Time.unscaledTime + 2f;
                _deadline = Time.unscaledTime + 30f;
                BtcLog.Info("RackFilter", "save has " + list.Count + " rack filter(s), restoring after the load");
            }
            catch (Exception e) { BtcLog.Error("RackFilter", "OnLoad: " + e); }
        }

        /// <summary>BtcTimer.Tick (host): put saved filters back on the nearest racks.</summary>
        internal static void LoadTick()
        {
            if (_pending == null || Time.unscaledTime < _applyAt) return;
            if (!NetworkServer.active || !Enabled) { _pending = null; return; }
            try
            {
                _applyAt = Time.unscaledTime + 1f;
                ToolRack[] all = BtcRegistry.RacksArray();
                List<Vector4> left = new List<Vector4>();
                int done = 0;
                for (int k = 0; k < _pending.Count; k++)
                {
                    ToolRack best = null;
                    float bestD = 1.5f * 1.5f;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] == null || all[i].recepteur == null) continue;
                        float d = (all[i].transform.position - (Vector3)_pending[k]).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = all[i]; }
                    }
                    if (best == null || KeyOf(best) <= 0) { left.Add(_pending[k]); continue; }
                    ApplyLocal(best, Mathf.RoundToInt(_pending[k].w));
                    done++;
                }
                if (done > 0) BtcRackFilterNet.Broadcast();
                _pending = left.Count > 0 && Time.unscaledTime < _deadline ? left : null;
                if (_pending == null) BtcLog.Info("RackFilter", "restore finished, " + done + " this pass, " + left.Count + " not found");
            }
            catch (Exception e) { _pending = null; BtcLog.Error("RackFilter", "LoadTick: " + e); }
        }

        // ------------------------------------------------------------------ pie DSL (Info/Livre text injected by Patches/LivrePatches.cs)
        internal static bool PieVisible()
        {
            return Enabled && TargetRack() != null && IsOwn(TargetRack());
        }

        internal static bool PieNotCurrent(int kit)
        {
            ToolRack r = TargetRack();
            return Enabled && r != null && IsOwn(r) && Get(r) != kit;
        }

        internal static void PieSet(int kit)
        {
            ToolRack r = TargetRack();
            if (r != null) Request(r, kit);
        }

        internal static void Install()
        {
            Instructions.conditions["Condition_Rack_Filter_Visible"] = () => PieVisible();
            Instructions.conditions["Condition_Rack_Filter_Not_Any"] = () => PieNotCurrent(Any);
            Instructions.commendes["Event_Rack_Filter_Any"] = () => PieSet(Any);
            for (int i = 0; i < KitNames.Length; i++)
            {
                int kit = i;
                Instructions.conditions["Condition_Rack_Filter_Not_" + KitNames[i]] = () => PieNotCurrent(kit);
                Instructions.commendes["Event_Rack_Filter_" + KitNames[i]] = () => PieSet(kit);
            }
        }
    }
}
