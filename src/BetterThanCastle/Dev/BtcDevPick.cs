#if DEV
using System;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Input;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV picking probe. "pickprobe [tag]": casts a ray at the nearest enemy unit (from above/behind, not from the camera)
    /// through every active-mode picker like Picking.AddActionPicker does, logs what each one hits and which picker would win,
    /// plus the unit's colliders and the fair-fog verdict (BtcFogFair.Hidden).</summary>
    internal static class BtcDevPick
    {
        internal static void Probe(string tag)
        {
            try
            {
                Faction f = User.LocalUser != null ? User.LocalUser.faction : null;
                if (f == null) { BtcLog.Warn("PickProbe", "no local faction"); return; }
                Labor target = null; Labor own = null;
                foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                {
                    if (l == null || l.faction == null) continue;
                    if (l.faction == f) { if (own == null) own = l; }
                    else if (f.IsEnemyTo(l.gameObject) && target == null) target = l;
                }
                if (target == null) { BtcLog.Warn("PickProbe", "no enemy unit"); return; }
                StringBuilder sb = new StringBuilder();
                sb.Append(tag).Append(" target=").Append(target.name).Append(" pos=").Append(target.transform.position)
                  .Append(" fog=").Append(BtcFogFair.Fog).Append(" hidden=").Append(BtcFogFair.Hidden(target.gameObject))
                  .Append(" foreignPlan=").Append(BtcFogFair.IsForeignPlan(target.gameObject))
                  .Append(" mode=").Append(InputModeController.GetCurrentMode());
                BtcLog.Info("PickProbe", sb.ToString());
                Vector3 c = target.transform.position + Vector3.up * 0.8f;
                foreach (Collider col in target.GetComponentsInChildren<Collider>(true))
                {
                    BtcLog.Info("PickProbe", tag + " collider " + col.name + " layer=" + (UnityLayer)col.gameObject.layer + " en=" + col.enabled
                        + " active=" + col.gameObject.activeInHierarchy + " trig=" + col.isTrigger + " center=" + col.bounds.center);
                    if (col.gameObject.layer == (int)UnityLayer.UnitsPicking && col.enabled) c = col.bounds.center;
                }
                Vector3 from = c + Vector3.up * 8f + (own != null ? (own.transform.position - c).normalized * 8f : Vector3.forward * 8f);
                Ray ray = new Ray(from, (c - from).normalized);
                float dist = Picking.Instance != null ? Picking.Instance.MaxDistance : 1000f;
                Picker win = null;
                foreach (Picker pk in UnityEngine.Object.FindObjectsOfType<Picker>())
                {
                    bool inMode = pk.RegisteredModes != null && pk.RegisteredModes.Contains(InputModeController.GetCurrentMode());
                    bool ok = pk.PlaceAtRaycast(ray, dist, null);
                    VoxelRaycastHit h = pk.RaycastHit;
                    GameObject go = h != null ? h.HitGameObject : null;
                    BtcLog.Info("PickProbe", tag + " picker " + pk.name + " type=" + pk.type + " prio=" + pk.actionPriority + " inMode=" + inMode
                        + " ok=" + ok + " hit=" + (go != null ? go.name + "/" + (UnityLayer)go.layer : "-") + " d=" + (h != null ? h.DistanceToHit.ToString("0.00") : "-")
                        + " comp=" + (pk.CurrentPickerComponent != null ? pk.CurrentPickerComponent.GetType().Name : "-"));
                    if (inMode && pk.type == PickerType.action && ok && (win == null || pk.actionPriority > win.actionPriority
                        || (pk.actionPriority == win.actionPriority && pk.RaycastHit.DistanceToHit < win.RaycastHit.DistanceToHit))) win = pk;
                }
                RaycastHit raw;
                bool any = Physics.Raycast(ray, out raw, dist, -1, QueryTriggerInteraction.Collide);
                BtcLog.Info("PickProbe", tag + " WIN=" + (win != null ? win.name : "-") + " raw=" + (any ? raw.collider.name + "/" + (UnityLayer)raw.collider.gameObject.layer + " d=" + raw.distance.ToString("0.00") : "-"));
            }
            catch (Exception e) { BtcLog.Error("PickProbe", e.ToString()); }
        }
    }
}
#endif
