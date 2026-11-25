using System;
using System.Reflection;
using Brix.Game.Components;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle
{
    // eco.ffRespawn: seconds a firefly waits at the home crystal before it turns back into a bricktron
    // (Firefly.captureTimer delay; vanilla 25, the original conquest map sets 60). -1 = map value.
    // eco.ffSpeed: multiplier of the firefly flight speed (vanilla travel 2, dash 15 blocks/s).
    internal static class BtcFireflyRules
    {
        private static int _iResp = -2, _iSpeed = -2;
        private static FieldInfo _fDelay;
        private static bool _init;

        internal static void AddRows()
        {
            BtcGameplay.Add("eco.ffRespawn", "eco", "Firefly respawn time (s)", "Время респавна из светлячка (с)", -1f, -1f, 300f, 1f, false,
                "How long a firefly waits at the home crystal before it becomes a bricktron again. -1 = map value (vanilla 25 s, original conquest 60 s). 0 = at once. Host decides.",
                "Сколько светлячок ждёт у домашнего кристалла, прежде чем снова стать бриктроном. -1 = как на карте (в ваниле 25 с, в conquest 60 с). 0 = сразу. Решает хост.");
            BtcGameplay.Add("eco.ffSpeed", "eco", "Firefly flight speed x", "Скорость полёта светлячков x", 1f, 0.1f, 10f, 0.05f, false,
                "Multiplier of how fast a dead bricktron's firefly flies to the crystal. 1 = vanilla (2 blocks per second).",
                "Множитель скорости, с которой светлячок погибшего бриктрона летит к кристаллу. 1 = ваниль (2 блока в секунду).");
        }

        private static float Val(ref int idx, string id, float def)
        {
            if (idx == -2) idx = BtcGameplay.Idx(id);
            float[] a = BtcGameplay.Active;
            return (idx >= 0 && a != null && idx < a.Length) ? a[idx] : def;
        }

        private static void Init()
        {
            _init = true;
            _fDelay = typeof(Brix.Lua.BrixTimer).GetField("_delay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_fDelay == null) BtcLog.Error("Firefly", "_delay missing");
        }

        // Firefly.OnStartClient starts captureTimer the moment a dead unit's firefly is emitted (the real respawn clock,
        // it runs during the flight), OnTriggerEnter starts it for the others. Setting the delay is safe while running.
        internal static void BeforeCapture(Firefly ff, string tag)
        {
            if (!_init) Init();
            float t = Val(ref _iResp, "eco.ffRespawn", -1f);
            if (t < 0f || _fDelay == null || ff.captureTimer == null) return;
            _fDelay.SetValue(ff.captureTimer, t);
        }

        // Firefly.WanderAround (the flight of a dead unit's firefly to the crystal) moves at a hardcoded 2 blocks/s.
        internal static void WanderExtra(Firefly ff)
        {
            float m = Val(ref _iSpeed, "eco.ffSpeed", 1f);
            if (m == 1f) return;
            ff.transform.position += ff.forward * (Time.deltaTime * 2f * (m - 1f));
        }

    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(Firefly), "OnTriggerEnter")]
    internal static class FireflyCapturePatch
    {
        private static void Prefix(Firefly __instance)
        {
            try { BtcFireflyRules.BeforeCapture(__instance, "TriggerEnter"); }
            catch (Exception e) { BtcLog.Error("Firefly", "capture: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Firefly), "OnStartClient")]
    internal static class FireflyStartTimerPatch
    {
        private static void Prefix(Firefly __instance)
        {
            try { BtcFireflyRules.BeforeCapture(__instance, "OnStartClient"); }
            catch (Exception e) { BtcLog.Error("Firefly", "start: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Firefly), "WanderAround")]
    internal static class FireflyWanderSpeedPatch
    {
        private static void Postfix(Firefly __instance)
        {
            try { BtcFireflyRules.WanderExtra(__instance); }
            catch (Exception e) { BtcLog.Error("Firefly", "wander: " + e.Message); }
        }
    }
}
