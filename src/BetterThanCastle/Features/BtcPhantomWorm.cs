using System.Collections.Generic;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantom wormholes (host only). Travel points: own crystals (rule phantom.worm: home crystal, captured shards; allied ones too with
    /// phantom.wormAlly) and wormhole wards (rule phantom.wardWorm, BtcWormWard). Navigation.NavigateToOrAway of a phantom: when the way
    /// through two points is clearly shorter than the direct way, the trip becomes "walk (or fly) to the nearest point -> appear at the point
    /// nearest to the goal -> walk (or fly) to the goal". Crystals and wormhole wards form one network. Distances are straight lines (cheap,
    /// the same for both choices). A trip that does not reach its entry point blocks wormholes for that phantom for 10 s, so a task's retry
    /// loop falls back to the direct way. Clients see the jump of the synced transform (BtcPhantom.ClientStep plays the arrival effect).
    /// Cooldown per phantom: phantom.wormCd. A point switched off from its pie menu (BtcWormWard.Off*) is ignored.
    /// </summary>
    internal static class BtcPhantomWorm
    {
        private sealed class Pt
        {
            internal FireflyNest Nest;   // a crystal, or null
            internal GameObject Ward;    // a wormhole ward, or null
            internal Vector3 Pos { get { return Nest != null ? Nest.transform.position : Ward.transform.position; } }
            internal string Name { get { return Nest != null ? Nest.name : Ward.name; } }
        }

        private sealed class Req
        {
            internal Navigation Nav;
            internal Location Loc;
            internal bool Exact, Precise, Aggressive;
            internal Pt A, B;
        }

        private const float MinTrip = 16f;      // shorter trips always go direct
        private const float Gain = 0.9f;        // the wormhole way must be at most 90% of the direct one (walking paths are longer than straight lines)...
        private const float Margin = 3f;        // ...minus a few blocks (walking to the point and out of it)
        private const float EntryReach = 5f;    // the phantom must stand this close to the entry cell to jump

        private static readonly Dictionary<int, float> ReadyAt = new Dictionary<int, float>();   // unit instance id -> Time.time it may jump again
        private static readonly List<Pt> Pts = new List<Pt>();
        private static bool _inner;            // building the legs of a wormhole trip: no wormhole decision inside
        internal static int Trips, Aborted;

        /// <summary>Any wormhole source is on (crystals or wormhole wards).</summary>
        internal static bool Any { get { return BtcPhantom.Worm || BtcWormWard.Active; } }

        internal static void Reset() { ReadyAt.Clear(); }

        private static bool Mine(Faction me, Faction f)
        {
            if (me == null || f == null) return false;
            if (ReferenceEquals(me, f)) return true;
            return BtcPhantom.WormAlly && me.AllyFactions != null && me.AllyFactions.Contains(f);
        }

        private static bool Usable(Pt p, Faction me)
        {
            if (p.Nest != null)
                return BtcPhantom.Worm && p.Nest.gameObject.activeInHierarchy && Mine(me, p.Nest.faction) && !BtcWormWard.NestOff(p.Nest);
            return p.Ward != null && BtcWormWard.Active && Mine(me, Faction.GetFaction(p.Ward)) && BtcWormWard.Usable(p.Ward);
        }

        /// <summary>Collects the candidate points (no ownership filter yet; Nearest applies Usable).</summary>
        private static int Collect()
        {
            Pts.Clear();
            if (BtcPhantom.Worm)
            {
                List<FireflyNest> list = BtcMatchStats.NestList();
                if (list != null) for (int i = 0; i < list.Count; i++) if (list[i] != null) Pts.Add(new Pt { Nest = list[i] });
            }
            if (BtcWormWard.Active)
            {
                List<GameObject> wards = BtcWormWard.Wards();
                for (int i = 0; i < wards.Count; i++) Pts.Add(new Pt { Ward = wards[i] });
            }
            return Pts.Count;
        }

        private static Pt Nearest(Faction me, Vector3 p, out float dist)
        {
            Pt best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Pts.Count; i++)
            {
                Pt n = Pts[i];
                if (!Usable(n, me)) continue;
                float d = (n.Pos - p).sqrMagnitude;
                if (d < bd) { bd = d; best = n; }
            }
            dist = best != null ? Mathf.Sqrt(bd) : 0f;
            return best;
        }

        /// <summary>A wormhole trip for this navigation, or null (go direct).</summary>
        internal static Node TryMake(Navigation nav, Location loc, bool exact, bool precise, bool aggressive)
        {
            if (_inner || loc == null || nav == null) return null;
            GameObject go = nav.gameObject;
            float t;
            if (ReadyAt.TryGetValue(go.GetInstanceID(), out t) && Time.time < t) return null;
            Vector3 p = go.transform.position, dst = loc.Position;
            float direct = Vector3.Distance(p, dst);
            if (direct < MinTrip) return null;
            if (Collect() < 2) return null;
            Faction me = Faction.GetFaction(go);
            float da, db;
            Pt a = Nearest(me, p, out da), b = Nearest(me, dst, out db);
            if (a == null || b == null || (a.Nest != null ? a.Nest == b.Nest : a.Ward == b.Ward)) return null;
            if (da + db + Margin > direct * Gain) return null;
            Req r = new Req { Nav = nav, Loc = loc, Exact = exact, Precise = precise, Aggressive = aggressive, A = a, B = b };
            return ActivityNode.Make<Req>(Run, r);
        }

        /// <summary>Entry/exit cell of a point: its central block (the walkable cell under the crystal / the ward cell) for phantoms that pass
        /// objects (ghost walk or flying, user 2026-11-17: "run into the crystal, not to the cells next to it"), else a free cell next to it.</summary>
        private static Vector3 Core(Pt pt, GameObject go)
        {
            bool through = BtcPhantomGhost.On(go) || BtcPhantom.Flies(go);
            if (pt.Ward != null)
            {
                Vector3 wp = pt.Ward.transform.position;
                try
                {
                    Brix.Pathfinding.Test.WalkablePathNode w = Brix.Game.AI.Pathfinding.PathNodeRegistry.WalkableAt(Brix.Engine.XYZ.FromVector3(wp + Vector3.up * 0.1f), through ? 1 : 3);
                    if (w != null) return w.RepresentativePosition.ToVector3();
                }
                catch (System.Exception e) { BtcLog.Warn("Phantom", "wormhole ward core: " + e.Message); }
                return wp;
            }
            FireflyNest n = pt.Nest;
            if (through)
            {
                try
                {
                    Vector3 c = Brix.Engine.Architecte.SurfaceRaycast(n.transform.position + Vector3.up * 4f, 10, true);
                    Brix.Pathfinding.Test.WalkablePathNode w = Brix.Game.AI.Pathfinding.PathNodeRegistry.WalkableAt(Brix.Engine.XYZ.FromVector3(c), 3);
                    if (w != null)
                    {
                        Vector3 r = w.RepresentativePosition.ToVector3();
                        Vector3 flat = r - n.transform.position; flat.y = 0f;
                        if (flat.sqrMagnitude <= 2.5f) return r;
                    }
                }
                catch (System.Exception e) { BtcLog.Warn("Phantom", "wormhole core: " + e.Message); }
            }
            return BtcPhantom.SpawnPoint(n);
        }

        private static Node Leg(Navigation nav, Location loc, bool exact, bool precise, bool aggressive)
        {
            _inner = true;
            try { return nav.NavigateTo(loc, exact, precise, aggressive); }
            finally { _inner = false; }
        }

        private static IEnumerator<ActivityProgress> Run(Req r)
        {
            Navigation nav = r.Nav;
            GameObject go = nav.gameObject;
            int id = go.GetInstanceID();
            Faction me = Faction.GetFaction(go);
            Vector3 entry = Core(r.A, go);
            ReadyAt[id] = Time.time + 10f;             // until the entry point is reached: a failed leg makes the retry go direct
            yield return ActivityProgress.Sub(Leg(nav, new SpaceLocation(entry, true), true, true, false));
            bool ok = go != null && BtcPhantom.IsPhantom(go) && Usable(r.A, me) && Usable(r.B, me)
                      && Vector3.Distance(go.transform.position, entry) <= EntryReach;
            if (ok)
            {
                Locomotion4 loco = nav.Locomotion;
                Vector3 exit = Core(r.B, go);
                exit.y += loco.FloorOffset;
                Color col = BtcPhantomLook.FxColor(go);
                BtcPhantomFx.Warp(go, col);
                loco.ClearPath();
                loco.SetPosition(exit, true);
                BtcPhantomFx.Warp(go, col);
                Trips++;
                ReadyAt[id] = Time.time + BtcPhantom.WormCd;
#if DEV
                BtcLog.Info("PhWorm", "trip id=" + id + " " + r.A.Name + " -> " + r.B.Name + " at=" + exit + " goal=" + r.Loc.Position);
#endif
            }
            else Aborted++;
            yield return ActivityProgress.Sub(Leg(nav, r.Loc, r.Exact, r.Precise, r.Aggressive));
        }
    }
}
