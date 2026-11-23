using System;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Utils.Signals;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // Health bars over units, after Castle Story Plus (Tricky12321, GPLv3: HealthBarRefresh). The bars (CharacterHighlightDriver,
    // ObjectsHighlightDriver) redraw only when HPChangedSignal fires, and the game fires it only when the health itself changes.
    // Gaps that left a bar with an old fill or none: a new maximum with the same health (a client's tool/armour swap sets the
    // maximum locally, the host's later value is the same); a unit already hurt when its bar connects (load, joining client, new
    // unit); a bar hidden in the fog and never redrawn when the unit comes out; the bar taken off a pooled unit only on its first
    // release. A redraw fires the signal with the same value twice: bars redraw, nothing flashes. Local display only, no net.
    internal static class BtcHealthBarRefresh
    {
        private static bool _logged;

        internal static void Redraw(DamageReceiver receiver)
        {
            try
            {
                if (receiver != null && receiver.MaxHP > 0f) receiver.HPChangedSignal.Invoke(receiver.CurrentHP, receiver.CurrentHP);
            }
            catch (Exception e) { Failed(e); }
        }

        internal static void RedrawBar(CharacterHighlightDriver driver, Labor labor)
        {
            try
            {
                if (driver != null && labor != null && labor.gameObject.activeInHierarchy) driver.RefreshCircle(labor);
            }
            catch (Exception e) { Failed(e); }
        }

        internal static void Failed(Exception e)
        {
            if (_logged) return;
            _logged = true;
            BtcLog.Warn("HpBar", "a health bar could not be redrawn: " + e.Message);
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(DamageReceiver), "MaxHP", MethodType.Setter)]
    internal static class HealthBarMaxPatch
    {
        private static void Prefix(DamageReceiver __instance, out float __state) { __state = __instance.MaxHP; }

        private static void Postfix(DamageReceiver __instance, float __state)
        {
            if (__instance.MaxHP != __state) BtcHealthBarRefresh.Redraw(__instance);
        }
    }

    /// <summary>Draws once for every unit the driver connects to; takes the bar off again on every release of a pooled unit.</summary>
    [HarmonyPatch(typeof(CharacterHighlightDriver), "ConnectBricktronSignals")]
    internal static class HealthBarConnectPatch
    {
        private static void Postfix(CharacterHighlightDriver __instance, Labor l)
        {
            BtcHealthBarRefresh.RedrawBar(__instance, l);
            if (l == null) return;
            l.OnReleaseOrDestroySignal.ConnectOnce(go =>
            {
                if (SelectionCircleDispenser.Instance != null && go != null) SelectionCircleDispenser.Instance.Remove(go, 0f);
            });
        }
    }

    [HarmonyPatch(typeof(CharacterHighlightDriver), "ConnectToSignals")]
    internal static class HealthBarFogPatch
    {
        private static void Postfix(CharacterHighlightDriver __instance)
        {
            try
            {
                UIGameObserver.Bricktron observer = __instance.observer;
                if (observer != null) observer.ExitFog.Connect(labor => BtcHealthBarRefresh.RedrawBar(__instance, labor)).DisconnectWhen(__instance.OnReleaseOrDestroySignal);
            }
            catch (Exception e) { BtcHealthBarRefresh.Failed(e); }
        }
    }
}
