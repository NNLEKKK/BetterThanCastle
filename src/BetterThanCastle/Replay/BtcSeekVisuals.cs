using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay v4 keyframes: client-only visual state that no network payload carries. CrystalVisuals (owner colour, creep colour and
    /// progression, capture transition) lives in the crystal Animator plus a few fields, driven by gameplay signals a jump never sends.
    /// Stored per crystal (keyed by the netId of its NetworkIdentity) and written back silently (no sounds).
    /// </summary>
    internal static class BtcSeekVisuals
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo CapProg = typeof(CrystalVisuals).GetField("captureProgress", Any);
        private static readonly FieldInfo LastProg = typeof(CrystalVisuals).GetField("lastCaptureProgress", Any);
        private static readonly FieldInfo TimeInc = typeof(CrystalVisuals).GetField("timeAtLastIncrement", Any);

        internal static byte[] Snap()
        {
            CrystalVisuals[] all = UnityEngine.Object.FindObjectsOfType<CrystalVisuals>();
            List<KeyValuePair<uint, CrystalVisuals>> l = new List<KeyValuePair<uint, CrystalVisuals>>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                NetworkIdentity ni = all[i].GetComponentInParent<NetworkIdentity>();
                if (ni != null) l.Add(new KeyValuePair<uint, CrystalVisuals>(ni.netId.Value, all[i]));
            }
            if (l.Count == 0) return null;
            l.Sort((a, b) => a.Key.CompareTo(b.Key));
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write(l.Count);
            for (int i = 0; i < l.Count; i++)
            {
                CrystalVisuals cv = l[i].Value;
                Animator an = cv.GetComponent<Animator>();
                float[] ps = an != null && an.isActiveAndEnabled ? BtcReplaySeek.ReadParams(an) : new float[0];
                w.Write(l[i].Key);
                w.Write(ps.Length);
                for (int k = 0; k < ps.Length; k++) w.Write(ps[k]);
                w.Write(cv.IsInTransition);
                w.Write(Get(CapProg, cv));
                w.Write(Get(LastProg, cv));
                w.Write(Time.fixedTime - Get(TimeInc, cv));
            }
            return ms.ToArray();
        }

        internal static int Apply(byte[] blob)
        {
            if (blob == null) return 0;
            BinaryReader r = new BinaryReader(new MemoryStream(blob));
            int n = r.ReadInt32(), done = 0;
            for (int i = 0; i < n; i++)
            {
                uint id = r.ReadUInt32();
                float[] ps = new float[r.ReadInt32()];
                for (int k = 0; k < ps.Length; k++) ps[k] = r.ReadSingle();
                bool tr = r.ReadBoolean();
                float cp = r.ReadSingle(), lp = r.ReadSingle(), age = r.ReadSingle();
                GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
                CrystalVisuals cv = go != null ? go.GetComponentInChildren<CrystalVisuals>(true) : null;
                if (cv == null) continue;
                Animator an = cv.GetComponent<Animator>();
                if (an != null && an.isActiveAndEnabled && ps.Length == an.parameters.Length) BtcReplaySeek.WriteParams(an, ps);
                // outside a transition normal viewing has the creep lerped up to the last progress; a fast feed can end it early
                if (!tr && an != null && an.isActiveAndEnabled && cp > 0f) cv.CreepProgression = cp;
                cv.IsInTransition = tr;
                Set(CapProg, cv, cp);
                Set(LastProg, cv, lp);
                Set(TimeInc, cv, Time.fixedTime - age);
                done++;
            }
            // the clocks' own memory (not synced): restored progress counts as already seen, so no false start/end follows
            foreach (CaptureClock cc in UnityEngine.Object.FindObjectsOfType<CaptureClock>())
            {
                CaptureProgress p = ClockCapture != null ? ClockCapture.GetValue(cc) as CaptureProgress : null;
                if (p == null) continue;
                p.previous = p.current;
                if (WasCaptured != null) WasCaptured.SetValue(cc, p.BeingCaptured());
                // capture circle, progress ring and the transition follow these signals: send the one normal viewing would have sent
                try
                {
                    IsCapturable ic = cc.GetComponent<IsCapturable>();
                    if (ic == null || UIGameObserver.capturables == null) continue;
                    bool ui = UIGameObserver.capturables.IsBeingCaptured(ic), now = p.BeingCaptured();
                    if (now && !ui && cc.CurrentCapturers != null) cc.CaptureStarted.Invoke(cc.CurrentCapturers);
                    else if (!now && ui) cc.CaptureEnded.Invoke();
                }
                catch (Exception e) { BtcLog.Warn("ReplaySeek", "capture signals: " + e.Message); }
            }
            return done;
        }

        private static readonly MethodInfo StRefresh = typeof(Brix.Game.Components.CraftingStation).GetMethod("RefeshInventory", Any);
        private static readonly MethodInfo StInv = typeof(Brix.Game.Components.CraftingStation).GetMethod("OnInventoryChanged", Any);
        private static readonly MethodInfo StQueue = typeof(Brix.Game.Components.CraftingStation).GetMethod("OnQueueChanged", Any);
        private static readonly MethodInfo StStart = typeof(Brix.Game.Components.CraftingStation).GetMethod("OnWorkStarted", Any);
        private static readonly MethodInfo StEnd = typeof(Brix.Game.Components.CraftingStation).GetMethod("OnWorkEnded", Any);
        private static readonly FieldInfo StState = typeof(Brix.Game.Components.CraftingStation).GetField("state", Any);

        private static readonly MethodInfo RcChanged = typeof(Brix.Game.Semantique.Recepteur).GetMethod("SetInventoryChanged", Any);
        private static readonly MethodInfo RcSingle = typeof(Brix.Game.Semantique.Recepteur).GetMethod("DealSingleResourceRecepteur", Any);
        internal static string LastRecepteurs = "";

        /// <summary>
        /// After a jump: container contents (Recepteur.StoredItems, altars, racks, stockpiles, units' inventories) are a client-side set kept
        /// by tracker hooks. A silent restore can skip the untrack of the old container (ForceHooks has no old value) and a pooled item
        /// re-used under another netId stays listed (altar "holding" a raw crystal instead of its ward). Truth = every networked item whose
        /// resource tracker points at the container: stale entries are removed, missing ones tracked the normal way.
        /// </summary>
        internal static int RebuildRecepteurs()
        {
            Dictionary<GameObject, HashSet<GameObject>> truth = new Dictionary<GameObject, HashSet<GameObject>>();
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects)
            {
                NetworkIdentity ni = kv.Value;
                if (ni == null || BtcReplaySeek.IsGhost(kv.Key.Value, ni)) continue;
                Brix.Game.Network.GenericParentTracker t = ni.GetComponent<Brix.Game.Network.GenericParentTracker>();
                if (t == null || t.TrackingType() != Brix.Game.Network.TrackingTypes.ressource || !t.IsTracked()) continue;
                GameObject p = t.TrackingParent();
                if (p == null) continue;
                HashSet<GameObject> set;
                if (!truth.TryGetValue(p, out set)) { set = new HashSet<GameObject>(); truth[p] = set; }
                set.Add(ni.gameObject);
            }
            int stale = 0, added = 0, boxes = 0;
            List<Brix.Game.Semantique.IDescriptor> drop = new List<Brix.Game.Semantique.IDescriptor>();
            foreach (Brix.Game.Semantique.Recepteur r in Resources.FindObjectsOfTypeAll<Brix.Game.Semantique.Recepteur>())
            {
                if (r == null || r.gameObject.scene.name == null) continue; // prefabs/assets
                HashSet<GameObject> want;
                truth.TryGetValue(r.gameObject, out want);
                drop.Clear();
                HashSet<GameObject> have = new HashSet<GameObject>();
                foreach (Brix.Game.Semantique.IDescriptor d in r.StoredItems)
                {
                    GameObject g = null;
                    try { g = d != null ? d.GameObject : null; } catch (Exception) { }
                    if (g == null || want == null || !want.Contains(g) || !have.Add(g)) drop.Add(d);
                }
                bool changed = false;
                for (int i = 0; i < drop.Count; i++)
                {
                    try { Brix.Game.Semantique.Recepteur.RemoveFrom(r, drop[i]); } catch (Exception) { r.StoredItems.Remove(drop[i]); }
                    stale++;
                    changed = true;
                }
                if (want != null)
                    foreach (GameObject g in want)
                    {
                        if (have.Contains(g)) continue;
                        try { r.OnObjectTracked(g); added++; changed = true; }
                        catch (Exception e) { BtcLog.Warn("ReplaySeek", "recepteur track " + g.name + ": " + e.Message); }
                    }
                if (!changed) continue;
                boxes++;
                try
                {
                    if (RcSingle != null) RcSingle.Invoke(r, null);
                    if (RcChanged != null) RcChanged.Invoke(r, null);
                }
                catch (Exception) { }
            }
            LastRecepteurs = "containers fixed " + boxes + " (stale " + stale + ", added " + added + ")";
            return boxes;
        }

        /// <summary>After a jump: crafting stations show their queued recipe (altar ward hologram), production and work effects only on
        /// change signals, which a silent restore never sends. Re-derive them from the restored state (virtual calls reach the subclasses).</summary>
        internal static int RefreshStations()
        {
            if (StRefresh == null || StInv == null || StQueue == null || StState == null) return -1;
            int n = 0;
            foreach (Brix.Game.Components.CraftingStation cs in UnityEngine.Object.FindObjectsOfType<Brix.Game.Components.CraftingStation>())
            {
                try
                {
                    StRefresh.Invoke(cs, null);
                    StInv.Invoke(cs, null);
                    StQueue.Invoke(cs, null);
                    Brix.Game.Components.CraftingState st = StState.GetValue(cs) as Brix.Game.Components.CraftingState;
                    MethodInfo w = st != null && st.Working ? StStart : StEnd;
                    if (w != null && st != null) w.Invoke(cs, null);
                    n++;
                }
                catch (Exception e) { BtcLog.Warn("ReplaySeek", "station refresh " + cs.name + ": " + (e.InnerException ?? e).Message); }
            }
            return n;
        }

        private static readonly FieldInfo ClockCapture = typeof(CaptureClock).GetField("capture", Any);
        private static readonly FieldInfo WasCaptured = typeof(CaptureClock).GetField("wasBeingCaptured", Any);

        private static float Get(FieldInfo f, object o)
        {
            return f != null ? (float)f.GetValue(o) : 0f;
        }

        private static void Set(FieldInfo f, object o, float v)
        {
            if (f != null) f.SetValue(o, v);
        }
    }
}
