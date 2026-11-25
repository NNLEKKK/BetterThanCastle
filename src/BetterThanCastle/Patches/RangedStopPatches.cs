using System;
using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using Brix.Utils;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Vanilla bug: a ranged unit that stopped to shoot (stance reached) re-checks CanFireAt with its real position
    /// (the approach check used the voxel-rounded one, or an ally stepped into the line meanwhile). When that fails,
    /// AttackInfo.DoRangedAction yields Fail(CombatTargetIsTooFarAway), the whole attack task dies and the unit stands idle
    /// one block short of its range. Fix: drop the ranged target instead, so the unit walks on toward the destination.</summary>
    internal static class RangedStop
    {
        private static int _logged;

        internal static IEnumerator<ActivityProgress> Wrap(IEnumerator<ActivityProgress> inner, AttackInfo self)
        {
            while (true)
            {
                bool more;
                try { more = inner.MoveNext(); }
                finally { }
                if (!more) yield break;
                ActivityProgress p = inner.Current;
                ActivityProgress.Failed f = p as ActivityProgress.Failed;
                if (f != null && f.Exception is Exceptions.CombatTargetIsTooFarAway && BtcNetworkGate.Enabled && Recover(self))
                {
                    yield return ActivityProgress.Wait();
                    yield break;
                }
                yield return p;
            }
        }

        private static bool Recover(AttackInfo self)
        {
            try
            {
                GameObject t = self.RangedTarget;
                if (t == null || self.Attacker == null) return false;
                // an attack with nowhere to walk (hold position) keeps vanilla behaviour: nothing to recover into
                if (!self.CanMove) return false;
                if (_logged++ < 40)
                {
                    ProjectileWeapon w = self.ProjectileWeapon;
                    Vector3 d = t.transform.position - self.Attacker.transform.position;
                    float flat = new Vector2(d.x, d.z).magnitude;
                    string why = "";
                    if (w != null)
                    {
                        why = " flat=" + flat.ToString("0.00") + " max=" + w.MaxRange.ToString("0.0") + " inRange=" + BtcWorld.InRange(w, self.Attacker.gameObject, t.transform.position)
                            + " reach=" + ProjectilePhysics.IsWithinReach(self.Profession.ProjectileSpeedMultiplier * w.Power, d) + " dy=" + d.y.ToString("0.0");
                    }
                    BtcLog.Info("RangedFix", "too far at stance: " + self.Attacker.name + " dist=" + d.magnitude.ToString("0.00") + why + ", walking on");
                }
                self.RangedTarget = null;
                self.NextRangedTargetFrame = self.CurrentFrame + 10;
                return true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "RangedStop.Recover: " + e.Message); return false; }
        }
    }

    [HarmonyPatch(typeof(AttackInfo), "DoRangedAction")]
    internal static class AttackInfoDoRangedActionPatch
    {
        private static void Postfix(AttackInfo __instance, ref IEnumerator<ActivityProgress> __result)
        {
            try { if (BtcNetworkGate.Enabled) __result = RangedStop.Wrap(__result, __instance); }
            catch (Exception e) { BtcLog.Error("Hook", "AttackInfo.DoRangedAction: " + e.Message); }
        }
    }
}
