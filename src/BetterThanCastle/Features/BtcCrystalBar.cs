using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Game;
using Brix.Game.Components;
using Brix.Game.AI.Damage;
using UnityEngine;

namespace BetterThanCastle
{
    // Home crystal health bar in real time: vanilla redraws the selection circle of a dynamic object only on HPChangedSignal / hover,
    // so a damaged crystal's bar stood still until the cursor touched it. Poll the crystals 5x per second and redraw the circle
    // when the health changed (or came back to full). Local display only, no net.
    internal static class BtcCrystalBar
    {
        private static readonly Dictionary<int, float> Last = new Dictionary<int, float>();
        private static MethodInfo _refresh;
        private static bool _tried;
        private static float _next;

        internal static void Tick()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            try
            {
                List<FireflyNest> nests = BtcMatchStats.NestList();
                if (nests == null || nests.Count == 0) { if (Last.Count > 0) Last.Clear(); return; }
                if (!_tried)
                {
                    _tried = true;
                    _refresh = typeof(ObjectsHighlightDriver).GetMethod("RefreshCircle", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Transform), typeof(bool) }, null);
                    if (_refresh == null) BtcLog.Warn("CrystalBar", "ObjectsHighlightDriver.RefreshCircle not found");
                }
                if (_refresh == null) return;
                for (int i = 0; i < nests.Count; i++)
                {
                    FireflyNest n = nests[i];
                    if (n == null) continue;
                    DamageReceiver dr = n.GetComponent<DamageReceiver>();
                    if (dr == null || dr.MaxHP <= 0f) continue;
                    int id = n.GetInstanceID();
                    float hp = dr.CurrentHP, was;
                    bool known = Last.TryGetValue(id, out was);
                    if (!known && !dr.IsDamaged) continue;
                    if (known && was == hp) continue;
                    Last[id] = hp;
                    _refresh.Invoke(null, new object[] { n.transform, known && hp < was });
                }
            }
            catch (Exception e) { BtcLog.Error("CrystalBar", "Tick: " + e.Message); }
        }
    }
}
