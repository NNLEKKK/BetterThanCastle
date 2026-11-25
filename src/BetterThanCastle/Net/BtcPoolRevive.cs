using System;
using System.Collections.Generic;
using Brix.Game.Components;
using Brix.Game.Network;
using Brix.Game.Semantique;
using Brix.Game.Storage;
using Brix.Utils.Signals;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Pooled objects come back without Start/Initialize (Unity runs them once per instance), but every signal those methods connected
    /// with DisconnectWhen(OnReleaseOrDestroySignal) was cut at the release. So a rack, stockpile or altar handed out again by the pool
    /// never updates its content display (RecepteurDisplay), fill animation (AnimatedProgression), scarf colour or station visuals.
    /// Vanilla rarely reuses buildings; replay seeks, rejoins and fog hide/show do it all the time. Here the same connections are
    /// made again when the pool hands out an instance whose components already started. Display parts always (local look only);
    /// CraftingStation (also logic) only where BtcTrackFix is on (BC ON or replay).
    /// </summary>
    internal static class BtcPoolRevive
    {
        private static readonly Dictionary<int, Component[]> Targets = new Dictionary<int, Component[]>();
        private static readonly List<Component> _buf = new List<Component>();

        private static readonly Action<AnimatedProgression> ApChanged = AccessTools.MethodDelegate<Action<AnimatedProgression>>(AccessTools.Method(typeof(AnimatedProgression), "InventoryChanged"));
        private static readonly Action<ScarfFactionColoring, GameObject> ScarfGo =
            AccessTools.MethodDelegate<Action<ScarfFactionColoring, GameObject>>(AccessTools.Method(typeof(ScarfFactionColoring), "TryUpdate", new[] { typeof(GameObject) }));
        private static readonly Action<ScarfFactionColoring, AffiliationComponent> ScarfAff =
            AccessTools.MethodDelegate<Action<ScarfFactionColoring, AffiliationComponent>>(AccessTools.Method(typeof(ScarfFactionColoring), "TryUpdate", new[] { typeof(AffiliationComponent) }));
        private static readonly AccessTools.FieldRef<CraftingStation, CraftingState> StState = AccessTools.FieldRefAccess<CraftingStation, CraftingState>("state");
        private static readonly Action<CraftingStation> StInv = AccessTools.MethodDelegate<Action<CraftingStation>>(AccessTools.Method(typeof(CraftingStation), "OnInventoryChanged"));
        private static readonly Action<CraftingStation> StData = AccessTools.MethodDelegate<Action<CraftingStation>>(AccessTools.Method(typeof(CraftingStation), "OnDataChanged"));
        private static readonly Action<CraftingStation> StStart = AccessTools.MethodDelegate<Action<CraftingStation>>(AccessTools.Method(typeof(CraftingStation), "OnWorkStarted"));
        private static readonly Action<CraftingStation> StWork = AccessTools.MethodDelegate<Action<CraftingStation>>(AccessTools.Method(typeof(CraftingStation), "OnWork"));
        private static readonly Action<CraftingStation> StEnd = AccessTools.MethodDelegate<Action<CraftingStation>>(AccessTools.Method(typeof(CraftingStation), "OnWorkEnded"));
        private static readonly Action<CraftingStation, int> StProc = AccessTools.MethodDelegate<Action<CraftingStation, int>>(AccessTools.Method(typeof(CraftingStation), "OnProcess"));
        private static int _revived;
        private static float _nextLog;

        /// <summary>AbstractGameObjectPool.OnGameObjectRequested postfix (every hand-out, new or reused).</summary>
        internal static void OnRequested(GameObject go)
        {
            if (go == null) return;
            Component[] list;
            int key = go.GetInstanceID();
            if (!Targets.TryGetValue(key, out list))
            {
                _buf.Clear();
                Collect<RecepteurDisplay>(go);
                Collect<AnimatedProgression>(go);
                Collect<ScarfFactionColoring>(go);
                Collect<CraftingStation>(go);
                list = _buf.Count == 0 ? new Component[0] : _buf.ToArray();
                Targets[key] = list;
            }
            for (int i = 0; i < list.Length; i++)
            {
                Component c = list[i];
                if (c == null) continue;
                try { Revive(c); }
                catch (Exception e) { BtcLog.Warn("PoolRevive", c.GetType().Name + " " + go.name + ": " + e.Message); }
            }
            if (_revived > 0 && Time.realtimeSinceStartup > _nextLog)
            {
                BtcLog.Info("PoolRevive", "reconnected " + _revived + " display/station components on reused pool objects");
                _revived = 0;
                _nextLog = Time.realtimeSinceStartup + 30f;
            }
        }

        private static void Collect<T>(GameObject go) where T : Component
        {
            T[] a = go.GetComponentsInChildren<T>(true);
            for (int i = 0; i < a.Length; i++) _buf.Add(a[i]);
        }

        private static void Revive(Component c)
        {
            RecepteurDisplay d = c as RecepteurDisplay;
            if (d != null)
            {
                if (!d.Started || d.targetRecepteur == null) return;
                d.targetRecepteur.Changed.Connect(delegate { d.enabled = true; }).DisconnectWhen(d.OnReleaseOrDestroySignal);
                d.enabled = true;
                _revived++;
                return;
            }
            AnimatedProgression ap = c as AnimatedProgression;
            if (ap != null)
            {
                if (!ap.Started || ap.recepteur == null) return;
                ap.recepteur.InventoryChanged.Connect(delegate { ApChanged(ap); }).DisconnectWhen(ap.OnReleaseOrDestroySignal);
                ApChanged(ap);
                _revived++;
                return;
            }
            ScarfFactionColoring sc = c as ScarfFactionColoring;
            if (sc != null)
            {
                if (!sc.Started) return;
                GenericParentTracker t = sc.GetComponent<GenericParentTracker>();
                if (t != null) t.ObjectTrackedByNewParentSignal.Connect(delegate(GameObject g) { ScarfGo(sc, g); }).DisconnectWhen(sc.OnReleaseOrDestroySignal);
                ScarfAff(sc, sc.GetComponentInParent<AffiliationComponent>());
                _revived++;
                return;
            }
            CraftingStation st = c as CraftingStation;
            if (st != null)
            {
                // Initialize ran in the first Awake only; Started = this instance already lived once
                if (!st.Started || !BtcTrackFix.On || st.recepteur == null) return;
                CraftingState s = StState(st);
                st.recepteur.InventoryChanged.Connect(delegate { StInv(st); }).DisconnectWhen(st.OnReleaseOrDestroySignal);
                if (s != null)
                {
                    s.Updated.Connect(delegate { StData(st); }).DisconnectWhen(st.OnReleaseOrDestroySignal);
                    s.WorkStarted.Connect(delegate(CraftingState x) { StStart(st); }, delegate(CraftingState x) { StWork(st); }, delegate(CraftingState x) { StEnd(st); }, st.OnReleaseOrDestroySignal);
                    s.Processing.Connect(delegate(int ms) { StProc(st, ms); }).DisconnectWhen(st.OnReleaseOrDestroySignal);
                }
                _revived++;
            }
        }
    }
}
