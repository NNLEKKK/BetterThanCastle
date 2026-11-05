using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.UI;
using Brix.Game.Beacons;
using Brix.Input;
using Brix.Legacy;
using Brix.NewUI.OverheadDisplay;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay: the tasks (projects) of ALL players as the game shows them to their owner. Recording: BeaconDriver.OnRebuildObservers also lists the
    /// recording connections (vanilla: owner faction only), so every project is part of the replay. Playback: every project shows its beacon with the
    /// game's own radius circle and, for flatten and quarry, the volume, threshold plane and dig/fill blocks, all with the game's colours. The owner is
    /// shown on the task plate (the name tag over the beacon): a frame, the icon badge and the text in the player's colour. The "BP+tasks" button
    /// switches it. Everything happens on game events, nothing runs per frame.
    /// </summary>
    internal static class BtcReplayTasks
    {
        private static readonly List<BeaconDriver> Drivers = new List<BeaconDriver>();
        private static readonly HashSet<int> DrvIds = new HashSet<int>();
        private static readonly List<Component> Ctrls = new List<Component>();
        private static readonly HashSet<int> CtrlIds = new HashSet<int>();
        private static MethodInfo _showBalise, _landRefresh, _quarryRefresh;
        private static FieldInfo _landUi, _quarryUi, _tagFill, _tagBadge, _tagName;
        private static bool _last = true;
        private static int _errs, _plates;

        internal static bool On
        {
            get { return BtcReplay.Mode == BtcReplay.State.Playing && (BtcConfig.ReplayInfo == null || BtcConfig.ReplayInfo.Value); }
        }

        // ------------------------------------------------------------------ recording
        internal static void AddObservers(BeaconDriver d, HashSet<NetworkConnection> observers)
        {
            if (BtcReplay.Mode != BtcReplay.State.Recording || d == null || d.project == null) return;
            BtcReplay.AddRecObservers(observers);
        }

        // ------------------------------------------------------------------ registry
        internal static void Register(BeaconDriver d)
        {
            if (d != null && DrvIds.Add(d.GetInstanceID())) Drivers.Add(d);
        }

        internal static void RegisterCtrl(Component c)
        {
            if (c != null && CtrlIds.Add(c.GetInstanceID())) Ctrls.Add(c);
        }

        // ------------------------------------------------------------------ beacon + plate
        /// <summary>BeaconDriver.ShowBaliseIfNeeded postfix.</summary>
        internal static void AfterShowBalise(BeaconDriver d)
        {
            if (!On || d == null) return;
            Register(d);
            try
            {
                if (d.project == null) return;
                Beacon b = d.Beacon;
                Faction f = d.faction;
                if (b == null || !BtcMatchStats.Tracks(f)) return;
                d.Showing = true;
                try { b.ShowingCircle = true; } catch (Exception) { }
                ColourPlate(b, f.FactionColor);
            }
            catch (Exception e) { Fail("beacon", e); }
        }

        /// <summary>BaliseOverheadDisplayDriver.InitValues postfix: the plate exists now.</summary>
        internal static void AfterPlateInit(BaliseOverheadDisplayDriver drv)
        {
            if (!On || drv == null) return;
            try
            {
                Beacon b = drv.GetComponent<Beacon>();
                if (b == null || b.project == null || !BtcMatchStats.Tracks(b.project.faction)) return;
                ColourPlate(b, b.project.faction.FactionColor);
            }
            catch (Exception e) { Fail("plate", e); }
        }

        private static void ColourPlate(Beacon b, Color fac)
        {
            BaliseOverheadDisplayDriver drv = b.GetComponent<BaliseOverheadDisplayDriver>();
            if (drv == null || drv.overheadDisplay == null || !drv.overheadDisplay.NameTag.IsSpawned) return;
            NameTag tag = drv.overheadDisplay.NameTag.Get;
            if (tag == null) return;
            if (_tagFill == null)
            {
                _tagFill = AccessTools.Field(typeof(NameTag), "FillImage");
                _tagBadge = AccessTools.Field(typeof(NameTag), "Badge");
                _tagName = AccessTools.Field(typeof(NameTag), "Name");
            }
            Color solid = new Color(fac.r, fac.g, fac.b, 1f);
            Image fill = _tagFill.GetValue(tag) as Image;
            if (fill != null)
            {
                Outline ol = fill.GetComponent<Outline>();
                if (ol == null) ol = fill.gameObject.AddComponent<Outline>();
                ol.effectColor = solid;
                ol.effectDistance = new Vector2(3f, -3f);
                ol.useGraphicAlpha = false;
            }
            Image badge = _tagBadge.GetValue(tag) as Image;
            if (badge != null) badge.color = solid;
            tag.TextColor(solid);
            _plates++;
        }

        // ------------------------------------------------------------------ landscape (flatten) / quarry volumes
        /// <summary>LandscapeController.RefreshVisibility postfix.</summary>
        internal static void AfterLandscape(Component ctrl)
        {
            if (!On || ctrl == null) return;
            RegisterCtrl(ctrl);
            try
            {
                if (_landUi == null) _landUi = AccessTools.Field(ctrl.GetType(), "ui");
                LandscapeProjectUI ui = _landUi.GetValue(ctrl) as LandscapeProjectUI;
                if (ui != null) ui.Showing = true;
            }
            catch (Exception e) { Fail("landscape", e); }
        }

        /// <summary>QuarryController.RefreshVisibility postfix.</summary>
        internal static void AfterQuarry(Component ctrl)
        {
            if (!On || ctrl == null) return;
            RegisterCtrl(ctrl);
            try
            {
                if (_quarryUi == null) _quarryUi = AccessTools.Field(ctrl.GetType(), "ui");
                QuarryProjectUI ui = _quarryUi.GetValue(ctrl) as QuarryProjectUI;
                if (ui != null) ui.Showing = true;
            }
            catch (Exception e) { Fail("quarry", e); }
        }

        private static void Fail(string what, Exception e)
        {
            if (++_errs <= 5) BtcLog.Error("ReplayTasks", what + ": " + e.Message);
        }

        // ------------------------------------------------------------------ toggle
        /// <summary>Update(): when the Blueprints button flips, every known project re-evaluates its visibility (the game's own rule when off).</summary>
        internal static void Tick()
        {
            if (BtcReplay.Mode != BtcReplay.State.Playing) { if (Drivers.Count > 0 || Ctrls.Count > 0) Clear(); return; }
            bool on = BtcConfig.ReplayInfo == null || BtcConfig.ReplayInfo.Value;
            if (on == _last) return;
            _last = on;
            Refresh();
        }

        private static void Refresh()
        {
            try
            {
                if (_showBalise == null) _showBalise = AccessTools.Method(typeof(BeaconDriver), "ShowBaliseIfNeeded");
                for (int i = Drivers.Count - 1; i >= 0; i--)
                {
                    if (Drivers[i] == null) { Drivers.RemoveAt(i); continue; }
                    _showBalise.Invoke(Drivers[i], null);
                }
                for (int i = Ctrls.Count - 1; i >= 0; i--)
                {
                    Component c = Ctrls[i];
                    if (c == null) { Ctrls.RemoveAt(i); continue; }
                    MethodInfo m;
                    if (c is LandscapeController) { if (_landRefresh == null) _landRefresh = AccessTools.Method(typeof(LandscapeController), "RefreshVisibility"); m = _landRefresh; }
                    else { if (_quarryRefresh == null) _quarryRefresh = AccessTools.Method(typeof(QuarryController), "RefreshVisibility"); m = _quarryRefresh; }
                    m.Invoke(c, null);
                }
            }
            catch (Exception e) { Fail("refresh", e); }
        }

        private static void Clear()
        {
            Drivers.Clear(); DrvIds.Clear(); Ctrls.Clear(); CtrlIds.Clear();
            _last = true;
        }

        /// <summary>DEV/diagnostics: one line with the current state.</summary>
        internal static string Status()
        {
            int land = 0, quar = 0, showing = 0;
            for (int i = 0; i < Ctrls.Count; i++) if (Ctrls[i] is LandscapeController) land++; else quar++;
            for (int i = 0; i < Drivers.Count; i++) if (Drivers[i] != null && Drivers[i].project != null) { try { if (Drivers[i].Showing) showing++; } catch { } }
            return "beacons " + Drivers.Count + " (showing " + showing + "), landscape " + land + ", quarry " + quar + ", plates coloured " + _plates;
        }
    }

    /// <summary>Recording: the REC connections also observe every project (BeaconDriver is on the project object and decides its observers).</summary>
    [HarmonyPatch(typeof(BeaconDriver), "OnRebuildObservers")]
    internal static class ReplayTasksObserversPatch
    {
        private static void Postfix(BeaconDriver __instance, HashSet<NetworkConnection> observers)
        {
            try { BtcReplayTasks.AddObservers(__instance, observers); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayTasksObservers: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(BeaconDriver), "ShowBaliseIfNeeded")]
    internal static class ReplayTasksBeaconPatch
    {
        private static void Postfix(BeaconDriver __instance)
        {
            try { BtcReplayTasks.AfterShowBalise(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayTasksBeacon: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(BaliseOverheadDisplayDriver), "InitValues")]
    internal static class ReplayTasksPlatePatch
    {
        private static void Postfix(BaliseOverheadDisplayDriver __instance)
        {
            try { BtcReplayTasks.AfterPlateInit(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayTasksPlate: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(LandscapeController), "RefreshVisibility")]
    internal static class ReplayTasksLandscapePatch
    {
        private static void Postfix(LandscapeController __instance)
        {
            try { BtcReplayTasks.AfterLandscape(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayTasksLandscape: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(QuarryController), "RefreshVisibility")]
    internal static class ReplayTasksQuarryPatch
    {
        private static void Postfix(QuarryController __instance)
        {
            try { BtcReplayTasks.AfterQuarry(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "ReplayTasksQuarry: " + e.Message); }
        }
    }
}
