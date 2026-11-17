using System;
using System.Collections.Generic;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Wards;
using Brix.Game.Semantique;
using Brix.Input;
using Brix.Network;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Build tasks for wormhole wards (BtcWormWard). The build menu (GameMenu.lua, wards section) gets a "Wormhole Ward" button: it holds
    /// the vanilla sentinel ward blueprint in "wormhole" mode. Every sentinel blueprint placed in that mode is tagged: the client sends
    /// its position (msg 120 "b|x|y|z"), the host matches it with the blueprint it spawned there (UNetBlueprint._FinishSpawnBlueprint).
    /// Builders fetch only wormhole wards for a tagged blueprint and only plain sentinel wards for an untagged one (Labor.Find context +
    /// Knowledge pickup test + Recepteur.FirstObjectOf). The finished blueprint spawns a new ward (Blueprint.MorphToObject): it is marked.
    /// Tags survive saves (lines "b x y z" in btc-wormwards.txt).
    /// </summary>
    internal static class BtcWormWardBuild
    {
        private struct Pend { internal Vector3 Pos; internal Faction F; internal float T; }

        private static bool _placing;                                   // local: the held sentinel blueprint is the wormhole one
        private static bool _ours;                                      // inside our own PlaceBlueprint call
        private static readonly Dictionary<int, GameObject> Tagged = new Dictionary<int, GameObject>();   // host: blueprint instance id -> blueprint
        private static readonly List<Pend> PendTags = new List<Pend>();      // host: tags whose blueprint is not spawned yet
        private static readonly List<Blueprint> Recent = new List<Blueprint>();   // host: sentinel blueprints spawned in the last seconds
        private static readonly List<float> RecentT = new List<float>();
        internal static List<Vector3> PendingLoad;                       // host: tags from a save
        private static float _loadUntil = -1f;

        /// <summary>Labor.Find context: 0 no filter, 1 plain sentinel ward wanted, 2 wormhole ward wanted.</summary>
        internal static int Want;

        // ------------------------------------------------------------------ local: build menu
        internal static void BeginPlacing()
        {
            if (!BtcWormWard.RuleOn) return;
            _ours = true;
            try { InputModeController.PlaceBlueprint(Blueprints.SentinelWard); }
            finally { _ours = false; }
            _placing = true;
        }

        /// <summary>InputModeController.PlaceBlueprint prefix: any other blueprint choice ends the wormhole mode.</summary>
        internal static void OnPlaceBlueprint() { if (!_ours) _placing = false; }

        internal static bool Placing { get { return _placing && BtcWormWard.RuleOn; } }

        private static DynValue LuaBuild(ScriptExecutionContext sec, CallbackArguments a) { BeginPlacing(); return DynValue.True; }
        private static DynValue LuaBuilding(ScriptExecutionContext sec, CallbackArguments a) { return DynValue.NewBoolean(Placing); }
        private static DynValue LuaOn(ScriptExecutionContext sec, CallbackArguments a) { return DynValue.NewBoolean(BtcWormWard.RuleOn); }
        internal static readonly DynValue BuildCb = DynValue.NewCallback(LuaBuild, "__BtcWormBuild");
        internal static readonly DynValue BuildingCb = DynValue.NewCallback(LuaBuilding, "__BtcWormBuilding");
        internal static readonly DynValue OnCb = DynValue.NewCallback(LuaOn, "__BtcWormBuildOn");

        internal static void SetGlobals(Table g)
        {
            if (g == null) return;
            g.Set("__BtcWormBuild", BuildCb);
            g.Set("__BtcWormBuilding", BuildingCb);
            g.Set("__BtcWormBuildOn", OnCb);
        }

        /// <summary>GameMenu.lua: a wormhole ward button right after the vanilla ward buttons (rule on at load).</summary>
        internal static string PatchGameMenu(string code)
        {
            string anchor = "m.AddMenuHandleToggle(h, _m.sh.buildStructuresWards) end)";
            int a = code.IndexOf(anchor, StringComparison.Ordinal);
            int e = a < 0 ? -1 : code.IndexOf("------", a, StringComparison.Ordinal);   // the line after the wards loop ("end end end")
            if (e < 0) { BtcLog.Error("WormWard", "GameMenu.lua wards anchor not found, no build button"); return code; }
            bool ru = BtcLang.Russian;
            string name = ru ? "Вард-червоточина" : "Wormhole Ward";
            string add = "\ndo\nif __BtcWormBuildOn and __BtcWormBuildOn() then\nlocal h = ButtonHandle.New()\n"
                + "h.Icon = function() return (__BtcWormIcon and __BtcWormIcon()) or IconKeys._Sentinel:Get64() end\n"
                + "h.IconColor = function() return White end\nh.IconSize = function() return 32 end\n"
                + "h.Label = function() return \"" + name + "\" end\nh.hasHotkey = false\nh.hasFgLabel = true\n"
                + "h.OnAction = function() return not UIGame.IsPaused() and __BtcWormBuild() end\n"
                + "h.Highlight = function() if __BtcWormBuilding() then return HighlightMode.Bottom end return HighlightMode.None end\n"
                + "h.FrontImage_Width = function() return 64 end\nh.FrontImage_Height = function() return 64 end\nh.onRefreshHighlight = Event.New()\n"
                + "Data.Project.ev_onSetHeldBlueprint:AddListener(function() return _m.mh.buildStructures:IsOpened() and h.onRefreshHighlight:Invoke() end)\n"
                + "_m.mh.buildStructures.onLoad:AddListener(function(m) m.AddMenuHandleToggle(h, _m.sh.buildStructuresWards) end)\n"
                + "_m.mh.buildStructures.onOpen:AddListener(function() return h.onRefreshHighlight:Invoke() end)\n"
                + "end\nend\n";
            BtcLog.Info("WormWard", "GameMenu.lua: build button added (rule " + (BtcWormWard.RuleOn ? "on" : "off") + " at load)");
            return code.Substring(0, e) + add + code.Substring(e);
        }

        // ------------------------------------------------------------------ placing (BlueprintPlacementPicker.PlaceBlueprint)
        internal static void OnPlaced(Blueprint held)
        {
            if (!Placing || held == null || held.AssetKey != Blueprints.SentinelWard) return;
            Vector3 p = held.transform.position;
            if (NetworkServer.active) Tag(p, User.LocalUser != null ? User.LocalUser.faction : null);
            else
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m != null && m.LocalNetworkClient != null && BtcNetworkGate.Enabled)
                    m.LocalNetworkClient.Send(BtcWormWard.MsgToggle, new StringMessage("b|" + F(p.x) + "|" + F(p.y) + "|" + F(p.z)));
            }
        }

        private static string F(float v) { return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Host: msg 120 "b|x|y|z" from a client.</summary>
        internal static void OnTagMsg(string[] a, Faction f)
        {
            float x, y, z;
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            if (a.Length < 4 || !float.TryParse(a[1], System.Globalization.NumberStyles.Float, inv, out x) || !float.TryParse(a[2], System.Globalization.NumberStyles.Float, inv, out y)
                || !float.TryParse(a[3], System.Globalization.NumberStyles.Float, inv, out z)) return;
            Tag(new Vector3(x, y, z), f);
        }

        private static void Tag(Vector3 p, Faction f)
        {
            if (!BtcWormWard.RuleOn) return;
            float now = Time.unscaledTime;
            for (int i = Recent.Count - 1; i >= 0; i--)
            {
                Blueprint b = Recent[i];
                if (b == null || (b.transform.position - p).sqrMagnitude > 0.04f || (f != null && b.faction != f)) continue;
                Mark(b);
                Recent.RemoveAt(i); RecentT.RemoveAt(i);
                return;
            }
            PendTags.Add(new Pend { Pos = p, F = f, T = now });
        }

        /// <summary>Host: UNetBlueprint._FinishSpawnBlueprint postfix (non-instant sentinel blueprints).</summary>
        internal static void OnSpawned(Blueprint b)
        {
            if (b == null || b.AssetKey != Blueprints.SentinelWard || !BtcWormWard.RuleOn) return;
            float now = Time.unscaledTime;
            for (int i = PendTags.Count - 1; i >= 0; i--)
                if (now - PendTags[i].T > 15f) PendTags.RemoveAt(i);
            for (int i = 0; i < PendTags.Count; i++)
            {
                Pend t = PendTags[i];
                if ((b.transform.position - t.Pos).sqrMagnitude > 0.04f || (t.F != null && b.faction != t.F)) continue;
                PendTags.RemoveAt(i);
                Mark(b);
                return;
            }
            for (int i = RecentT.Count - 1; i >= 0; i--)
                if (now - RecentT[i] > 15f || Recent[i] == null) { Recent.RemoveAt(i); RecentT.RemoveAt(i); }
            Recent.Add(b); RecentT.Add(now);
        }

#if DEV
        internal static void DevTag(Vector3 p, Faction f) { Tag(p, f); }
#endif

        private static void Mark(Blueprint b)
        {
            Tagged[b.gameObject.GetInstanceID()] = b.gameObject;
            BtcLog.Info("WormWard", "blueprint tagged at " + b.transform.position);
        }

        internal static bool IsTagged(GameObject bp) { return bp != null && Tagged.Count > 0 && Tagged.ContainsKey(bp.GetInstanceID()); }

        internal static int TaggedCount { get { return Tagged.Count; } }

        internal static void Clear() { Tagged.Clear(); PendTags.Clear(); Recent.Clear(); RecentT.Clear(); }

        // ------------------------------------------------------------------ builders pick the right kind
        /// <summary>Labor.Find prefix: the wanted kind for this target (a sentinel blueprint), else no filter.</summary>
#if DEV
        internal static bool DevNoFilter;
#endif
        internal static void BeginFind(Recepteur target)
        {
            Want = 0;
#if DEV
            if (DevNoFilter) return;
#endif
            if (!BtcWormWard.RuleOn || target == null) return;
            Blueprint b = target.GetComponent<Blueprint>();
            if (b == null || b.AssetKey != Blueprints.SentinelWard) return;
            Want = IsTagged(b.gameObject) ? 2 : 1;
        }

        private static bool Right(GameObject go)
        {
            return go.GetComponent<WardAttack>() == null || BtcWormWard.IsWorm(go) == (Want == 2);
        }

        /// <summary>Knowledge._ClosestObjectToPickup_Test: false = this candidate must not be used for the current target.</summary>
        internal static bool Accept(GameObject gc, Recepteur rec)
        {
            if (Want == 0 || gc == null) return true;
            if (rec == null || rec.Container == null) return Right(gc);     // a loose item
            Transform c = rec.Container;                                    // a stockpile/container: needs one of the right kind
            for (int i = 0; i < c.childCount; i++)
            {
                GameObject o = c.GetChild(i).gameObject;
                if (o.GetComponent<WardAttack>() != null && Right(o)) return true;
            }
            return false;
        }

        /// <summary>Recepteur.FirstObjectOf(Ressource) postfix while a builder looks for a ward: swap a wrong-kind ward for a right one.</summary>
        internal static GameObject Pick(Recepteur rec, GameObject first)
        {
            if (Want == 0 || first == null || first.GetComponent<WardAttack>() == null || Right(first)) return first;
            Transform c = rec != null ? rec.Container : null;
            if (c != null)
                for (int i = 0; i < c.childCount; i++)
                {
                    GameObject o = c.GetChild(i).gameObject;
                    if (o.GetComponent<WardAttack>() != null && Right(o)) return o;
                }
            return null;
        }

        // ------------------------------------------------------------------ completion
        /// <summary>Blueprint.MorphToObject prefix: -1 = refuse (a tagged blueprint got a plain ward: the ward is dropped next to it),
        /// 1 = the result is a wormhole ward (a wormhole ward was delivered, or a tagged blueprint finished without an item, e.g. instant),
        /// 0 = plain.</summary>
        internal static int BeforeMorph(Blueprint b)
        {
            if (b == null || b.AssetKey != Blueprints.SentinelWard) return 0;
            bool tagged = IsTagged(b.gameObject);
            GameObject delivered = null;
            Transform c = b.recepteur != null ? b.recepteur.Container : null;
            if (c != null)
                for (int i = 0; i < c.childCount && delivered == null; i++)
                    if (c.GetChild(i).GetComponent<WardAttack>() != null) delivered = c.GetChild(i).gameObject;
            bool worm = delivered != null && BtcWormWard.IsWorm(delivered);
            if (tagged && delivered != null && !worm)
            {
                BtcLog.Info("WormWard", "wormhole blueprint at " + b.transform.position + " refused a plain sentinel ward (dropped)");
                b.recepteur.DropAll();
                return -1;
            }
            return worm || (tagged && delivered == null) ? 1 : 0;
        }

        internal static void OnMorphed(GameObject blueprint, GameObject result)
        {
            Tagged.Remove(blueprint.GetInstanceID());
            if (result != null && result.GetComponent<WardAttack>() != null) BtcWormWard.Mark(result, false);
        }

        /// <summary>Recepteur.AddObject prefix: true = refuse (a plain sentinel ward into a tagged blueprint).</summary>
        internal static bool RefuseAdd(Recepteur rec, GameObject item)
        {
            if (item.GetComponent<WardAttack>() == null || BtcWormWard.IsWorm(item)) return false;
            Blueprint b = rec.GetComponent<Blueprint>();
            if (b == null || !IsTagged(b.gameObject)) return false;
            if (Time.unscaledTime >= _nextRefuseLog) { _nextRefuseLog = Time.unscaledTime + 5f; BtcLog.Info("WormWard", "wormhole blueprint at " + b.transform.position + " refused a plain sentinel ward"); }
            return true;
        }

        private static float _nextRefuseLog;

        /// <summary>Labor.Find postfix for a sentinel blueprint: the found ward must be of the wanted kind; else the nearest free ward of that
        /// kind of the worker's faction (loose or stored, not carried, not in another blueprint), or nothing (the blueprint waits).</summary>
        internal static GameObject FixFind(Labor l, Recepteur target, GameObject found)
        {
            if (target == null) return found;
            Blueprint b = target.GetComponent<Blueprint>();
            if (b == null || b.AssetKey != Blueprints.SentinelWard) return found;
            bool wantWorm = IsTagged(b.gameObject);
            if (found != null && (found.GetComponent<WardAttack>() == null || BtcWormWard.IsWorm(found) == wantWorm)) return found;
            if (!wantWorm) return found;      // plain blueprint: vanilla choice (a wormhole ward fed to it gives a wormhole ward, nothing lost)
            GameObject best = null;
            float bd = float.MaxValue;
            Faction f = l.faction;
            foreach (GameObject g in BtcWormWard.AllMarked())
            {
                if (g == null || Faction.GetFaction(g) != f) continue;
                Transform par = g.transform.parent;
                if (par != null && (par.GetComponentInParent<Labor>() != null || par.GetComponentInParent<Blueprint>() != null)) continue;
                float d = (g.transform.position - l.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        /// <summary>Transaction.StoreEverything prefix: a sentinel ward moves as exactly that object. True = done here.</summary>
        internal static bool StoreExact(GameObject from, GameObject into)
        {
            if (from.GetComponent<WardAttack>() == null) return false;
            GameComponent gc = into.GetComponent<GameComponent>();
            Recepteur r = gc != null ? gc.recepteur : null;
            if (r == null || !r.HasRoomFor(from)) return false;
            r.AddObject(from);
            return true;
        }

        // ------------------------------------------------------------------ saves (BtcWormWard sidecar)
        internal static void AppendSave(System.Text.StringBuilder sb, ref int n)
        {
            foreach (GameObject g in Tagged.Values)
            {
                if (g == null) continue;
                Vector3 p = g.transform.position;
                System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append("b ").Append(p.x.ToString("F2", inv)).Append(' ').Append(p.y.ToString("F2", inv)).Append(' ').Append(p.z.ToString("F2", inv)).Append('\n');
                n++;
            }
        }

        /// <summary>Host after a load: tag the sentinel blueprints standing where the save had tagged ones.</summary>
        internal static void ApplyLoad()
        {
            if (PendingLoad == null) return;
            if (_loadUntil < 0f) _loadUntil = Time.unscaledTime + 30f;
            else if (Time.unscaledTime > _loadUntil) { BtcLog.Warn("WormWard", "load: " + PendingLoad.Count + " tagged blueprint(s) not found"); PendingLoad = null; _loadUntil = -1f; return; }
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>())
            {
                if (b.AssetKey != Blueprints.SentinelWard || IsTagged(b.gameObject)) continue;
                for (int i = 0; i < PendingLoad.Count; i++)
                    if ((b.transform.position - PendingLoad[i]).sqrMagnitude < 0.25f) { Mark(b); PendingLoad.RemoveAt(i); break; }
            }
            if (PendingLoad.Count == 0) { PendingLoad = null; _loadUntil = -1f; }
        }
    }
}
