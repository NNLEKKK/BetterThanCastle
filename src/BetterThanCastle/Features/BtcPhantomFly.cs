using System.Collections.Generic;
using Brix.Engine;
using Brix.Game.AI;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Flying phantoms (rule phantom.fly, host only: locomotion runs on the server, clients get the synced transform).
    /// Navigation.NavigateToOrAway of a flying phantom is replaced by one activity node: pick the free cell nearest to the target where
    /// Location.TargetReached holds (the same predicate the vanilla path search and IsAt use, so tasks and their retry loops accept it),
    /// move there in a straight line at the unit's run/walk speed through anything, done. No pathfinder, so unreachable places work.
    /// While it flies the animator root motion is ignored; a flying phantom never snaps to the floor or falls (PhantomFly* patches).
    /// </summary>
    internal static class BtcPhantomFly
    {
        private sealed class Req
        {
            internal Navigation Nav;
            internal Location Loc;
            internal bool Away;
        }

        private const int MaxRing = 6;
        private static readonly Dictionary<int, float> LastStep = new Dictionary<int, float>();   // unit instance id -> Time.time of its last flight step
        internal static int Flights, Arrived, Unfindable, Jumps;

        /// <summary>The unit moved by a flight step within the last moments (its animator root motion is skipped then).</summary>
        internal static bool InFlight(GameObject go)
        {
            float t;
            return LastStep.Count > 0 && LastStep.TryGetValue(go.GetInstanceID(), out t) && Time.time - t < 0.15f;
        }

        internal static void Reset() { LastStep.Clear(); }

        internal static Node Make(Navigation nav, Location location, bool away)
        {
            Req r = new Req();
            r.Nav = nav; r.Loc = location; r.Away = away;
            return ActivityNode.Make<Req>(Run, r);
        }

        private static IEnumerator<ActivityProgress> Run(Req r)
        {
            Navigation nav = r.Nav;
            if (nav == null || r.Loc == null) { yield return ActivityProgress.Sub(Node.FailPathTo<Exceptions.PathInvalid>(r.Loc)); yield break; }
            Locomotion4 loco = nav.Locomotion;
            GameObject go = nav.gameObject;
            Transform tf = go.transform;
            float waited = 0f;
            while (!loco.CanMove())   // like the vanilla Validate: busy (working, sitting, hurt) for up to 1 s
            {
                if (waited > 1f) { yield return ActivityProgress.Sub(Node.FailPathTo<Exceptions.CantMove>(r.Loc)); yield break; }
                waited += Time.deltaTime;
                yield return ActivityProgress.Wait();
            }
            loco.ClearPath();
            Vector3 dest;
            if (!Resolve(go, loco, r, out dest))
            {
                Unfindable++;
                yield return ActivityProgress.Sub(Node.FailPathTo<Exceptions.PathUnfindable>(r.Loc));
                yield break;
            }
            Flights++;
            int id = go.GetInstanceID();
            Vector3 aim = r.Loc.Position;
            float reAt = Time.time + 0.25f;
#if DEV
            BtcLog.Info("PhFly", "start id=" + id + " loc=" + r.Loc.GetType().Name + " away=" + r.Away + " at=" + tf.position + " dest=" + dest);
            bool done = false;
            bool _hasLast = false;
            Vector3 _last = tf.position;
            try {
#endif
            while (true)
            {
                if (go == null || !BtcPhantom.Flies(go)) yield break;   // rule turned off or the phantom died: the caller retries on foot
                if (Time.time >= reAt)
                {
                    reAt = Time.time + 0.25f;
                    Vector3 now = r.Loc.Position;
                    if ((now - aim).sqrMagnitude > 0.25f) { aim = now; Vector3 d2; if (Resolve(go, loco, r, out d2)) dest = d2; }   // moving target
                }
                Vector3 cur = tf.position;
                Vector3 d = dest - cur;
                float step = (loco.CanRun ? loco.RunSpeedMperS : loco.WalkSpeedMperS) * Time.deltaTime;
#if DEV
                if (_hasLast && (cur - _last).sqrMagnitude > 1f) { Jumps++; BtcLog.Info("PhFly", "jump id=" + id + " " + _last + " -> " + cur); }
                _hasLast = true;
#endif
                LastStep[id] = Time.time;
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                if (flat.sqrMagnitude > 0.0004f) tf.rotation = Quaternion.LookRotation(flat, Vector3.up);
                if (d.sqrMagnitude <= step * step) { loco.SetPosition(dest, true); break; }
                loco.SetPosition(cur + d.normalized * step, true);
#if DEV
                _last = tf.position;
#endif
                yield return ActivityProgress.Wait();
            }
            Arrived++;
#if DEV
            done = true;
            } finally { BtcLog.Info("PhFly", (done ? "arrived" : "aborted") + " id=" + id + " at=" + (go != null ? go.transform.position.ToString() : "-")); }
#endif
        }

        private static Vector3 Round(Vector3 p) { return new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), Mathf.Round(p.z)); }

        private static bool Open(GameObject go, Vector3 c, bool units) { return !FloorPicker.IsOccupiedOrInsideGround(go, c, units); }

        /// <summary>Where to fly: the free cell nearest to the target that reaches it (head room preferred), else the free cell nearest to the
        /// target (the caller's IsAt check then fails like an unreached vanilla path). Away: 15 blocks straight away from the location.</summary>
        private static bool Resolve(GameObject go, Locomotion4 loco, Req r, out Vector3 dest)
        {
            Vector3 me = go.transform.position;
            float fo = loco.FloorOffset;
            Vector3 target = r.Loc.Position;
            bool useReach = true;
            if (r.Away)
            {
                Vector3 dir = me - target; dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) { dir = go.transform.forward; dir.y = 0f; }
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                target = me + dir.normalized * 15f;
                useReach = false;
            }
            else if (r.Loc.TargetReached(go, Round(me)))
            {
                dest = Round(me); dest.y += fo;
                return true;
            }
            Vector3 t = Round(target);
            bool haveFree = false;
            Vector3 free = t;
            float freeD = float.MaxValue;
            for (int ring = 0; ring <= MaxRing; ring++)
            {
                bool have = false;
                Vector3 best = t;
                float bestD = float.MaxValue;
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dy = -ring; dy <= ring; dy++)
                        for (int dz = -ring; dz <= ring; dz++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Max(Mathf.Abs(dy), Mathf.Abs(dz))) != ring) continue;
                            Vector3 c = new Vector3(t.x + dx, t.y + dy, t.z + dz);
                            if (!Open(go, c, true)) continue;
                            float dm = (c - me).sqrMagnitude;
                            bool head = Open(go, c + Vector3.up, false);
                            if (!haveFree || (head ? 0f : 1000f) + ring * 100f + dm < freeD) { haveFree = true; free = c; freeD = (head ? 0f : 1000f) + ring * 100f + dm; }
                            if (!useReach || !r.Loc.TargetReached(go, c)) continue;
                            float score = (head ? 0f : 10000f) + dm;
                            if (!have || score < bestD) { have = true; best = c; bestD = score; }
                        }
                if (have) { dest = best; dest.y += fo; return true; }
                if (!useReach && haveFree) break;
            }
            dest = free; dest.y += fo;
            return haveFree;
        }
    }
}
