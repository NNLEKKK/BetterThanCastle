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
