using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components.Volumes;
using Brix.Pathfinding;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantoms walk through objects (rule phantom.ghost, host only, off while phantom.fly flies them): the vanilla path search and walk
    /// (same branch types, same jumps, climbs and step downs as a worker), but every obstacle volume that is not made of blocks (buildings
    /// incl. enemy gates and doors, any placed object) is ignored and enemy bricktrons do not block; own ones block exactly as for a vanilla
    /// worker. Terrain, bricks, beams, tree trunks, boulders, iron and pyrite stay as vanilla (user 2026-11-17: "same path search, but through
    /// objects"; the forest branch type is NOT used: it ranks below Breaking and let the search plan paths that need digging).
    /// </summary>
    internal static class BtcPhantomGhost
    {
        /// <summary>The unit is a live phantom that walks through objects.</summary>
        internal static bool On(GameObject go) { return BtcPhantom.Ghost && go != null && BtcPhantom.IsPhantom(go); }

        /// <summary>An obstacle volume that still stops a ghost phantom: made of blocks (multi-block cells, block volumes without an
        /// object). Passed: anything with a GameObject (buildings, placed objects) and gates.</summary>
        private static bool Solid(IVolume v)
        {
            return v.GameObject == null && !(v.PassThrough is Brix.Legacy.Porte);
        }

        // only block-made obstacles stop a ghost phantom
        private static readonly Func<IVolume, GameObject, bool> SearchBlock = (v, g) => Solid(v) && !v.PassThrough.CanSearchThrough(g);
        private static readonly Func<IVolume, GameObject, bool> PassBlock = (v, g) => Solid(v) && v.GameObject != g && !v.PassThrough.CanPassThrough(g);
        private static readonly Func<IVolume, GameObject, bool> PassAny = (v, g) => v.GameObject != g && !BtcPhantomBody.Ignores(g, v.GameObject) && !v.PassThrough.CanPassThrough(g);

        /// <summary>SearchPathRequest.HasBlockerAt for a ghost phantom.</summary>
        internal static bool SearchBlocked(GameObject go, XYZ p)
        {
            return VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(p, VProperty.obstacle, SearchBlock, go);
        }

        /// <summary>The obstacle part of a walk test (repath search, FloorPicker.IsOccupied) for a ghost phantom.</summary>
        internal static bool PassBlocked(GameObject go, XYZ p)
        {
            return VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(p, VProperty.obstacle, PassBlock, go);
        }

        /// <summary>FloorPicker.IsOccupied for a ghost phantom: block obstacles, and other characters as vanilla when asked.</summary>
        internal static bool Occupied(GameObject go, XYZ p, bool characters)
        {
            if (PassBlocked(go, p)) return true;
            return characters && VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(p, VProperty.character, PassAny, go);
        }

        // ------------------------------------------------------------------ presence test (VoxelPresence.BuildPresenceTest postfix)
        private static readonly Dictionary<int, Func<XYZ, bool>[]> Tests = new Dictionary<int, Func<XYZ, bool>[]>();
        private static readonly Func<IVolume, VoxelPresence, bool> BlocksOwn = (v, s) => Blocks(v, s, false);
        private static readonly Func<IVolume, VoxelPresence, bool> BlocksOwnUnlessWork = (v, s) => Blocks(v, s, true);

        /// <summary>Vanilla presence test (everyone blocks, or allies pass when the caller asked for "pass through to work") with enemies
        /// never blocking. The vanilla aggressive test would also make allies block a working phantom. Cached per unit and mode.</summary>
        internal static Func<XYZ, bool> PresenceTest(VoxelPresence self, bool passAllies)
        {
            int id = self.GetInstanceID();
            Func<XYZ, bool>[] f;
            if (!Tests.TryGetValue(id, out f)) { f = new Func<XYZ, bool>[2]; Tests[id] = f; }
            int k = passAllies ? 1 : 0;
            if (f[k] == null)
            {
                VoxelPresence s = self;
                Func<IVolume, VoxelPresence, bool> b = passAllies ? BlocksOwnUnlessWork : BlocksOwn;
                f[k] = p => !VolumeRegistry.HasVolumesWithPropertyAt<VoxelPresence>(p, VProperty.character, b, s);
            }
            return f[k];
        }

        private static bool Blocks(IVolume v, VoxelPresence self, bool passAllies)
        {
            VoxelPresence o = v.GetComponent<VoxelPresence>();
            if (o == null || o == self || BtcPhantomBody.Ignores(self.gameObject, o.gameObject)) return false;
            if (o.affiliation.IsEnemy(self)) return false;
            return !(passAllies && o.OthersCanPassThroughMe && o.affiliation.IsAllied(self));
        }

        internal static void Reset() { Tests.Clear(); }
    }

    /// <summary>
    /// Phantom bodies (phantom.ghost or phantom.fly on, host): a phantom and a normal bricktron never touch. Neither blocks the other's
    /// path or step, both may stand in one cell, landing on the other's head neither stomps nor flails, and neither unstacks (steps aside)
    /// because of the other. Allied phantoms ignore each other too; only enemy phantoms block and push each other (user 2026-11-17).
    /// </summary>
    internal static class BtcPhantomBody
    {
        /// <summary>The rule applies right now (cheap: every hook asks it first).</summary>
        internal static bool On { get { return BtcPhantom.LiveCount > 0 && (BtcPhantom.Ghost || BtcPhantom.Fly); } }

        /// <summary>a and b do not touch: exactly one of them is a phantom, or both are phantoms of the same or allied factions
        /// (user 2026-11-17: allied phantoms blocked each other in narrow places; enemy phantoms still collide).</summary>
        internal static bool Ignores(GameObject a, GameObject b)
        {
            if (!On || a == null || b == null) return false;
            bool pa = BtcPhantom.IsPhantom(a), pb = BtcPhantom.IsPhantom(b);
            if (pa != pb) return true;
            if (!pa) return false;
            Faction fa = Faction.GetFaction(a), fb = Faction.GetFaction(b);
            if (fa == null || fb == null) return false;
            return ReferenceEquals(fa, fb) || (fa.AllyFactions != null && fa.AllyFactions.Contains(fb));
        }

        private static readonly Func<IVolume, GameObject, bool> Touches = (v, g) => v.GameObject != null && v.GameObject != g && !Ignores(g, v.GameObject);

        /// <summary>A character in this cell that the unit touches (not itself, not ignored).</summary>
        internal static bool AnyTouching(GameObject self, XYZ p)
        {
            return VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(p, VProperty.character, Touches, self);
        }

        /// <summary>First presence in the cell the unit touches, or null.</summary>
        internal static VoxelPresence FirstTouching(GameObject self, XYZ p)
        {
            foreach (IVolume v in VolumeRegistry.VolumesWithPropertyAt(p, VProperty.character))
            {
                GameObject g = v.GameObject;
                if (g == null || g == self || Ignores(self, g)) continue;
                VoxelPresence o = g.GetComponent<VoxelPresence>();
                if (o != null) return o;
            }
            return null;
        }

        /// <summary>FloorPicker.IsOccupied for a normal unit while phantoms are around: vanilla, but phantoms are no characters for it.</summary>
        private static readonly Func<IVolume, GameObject, bool> PassAnyObstacle = (v, g) => v.GameObject != g && !v.PassThrough.CanPassThrough(g);
        private static readonly Func<IVolume, GameObject, bool> PassAnyCharacter = (v, g) => v.GameObject != g && !Ignores(g, v.GameObject) && !v.PassThrough.CanPassThrough(g);

        internal static bool Occupied(GameObject go, XYZ p, bool characters)
        {
            if (VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(p, VProperty.obstacle, PassAnyObstacle, go)) return true;
            return characters && VolumeRegistry.HasVolumesWithPropertyAt<GameObject>(p, VProperty.character, PassAnyCharacter, go);
        }

        // ------------------------------------------------------------------ forest for ghost phantoms (SearchPathRequest.BranchEvaluator)
        /// <summary>Evaluators of ghost phantom searches: they also accept Forest branches (walk through foliage), without lowering
        /// AcceptedType to Forest (that would accept Breaking and Ladder branches too = paths through blocks). Trunks are nature
        /// blockers outside the walk graph and stay solid.</summary>
        internal static readonly HashSet<object> ForestEval = new HashSet<object>();
        private static readonly HarmonyLib.AccessTools.FieldRef<DestinationRepathSearch, GameObject> FRepathGo =
            HarmonyLib.AccessTools.FieldRefAccess<DestinationRepathSearch, GameObject>("_gameObject");

        /// <summary>Path searches (SearchPathRequest) and the short repath of a blocked step (DestinationRepathSearch, set up in its Init
        /// right after _gameObject): without the forest in the repath, a ghost phantom whose step into the foliage was checked again fell
        /// back to a full new search and stood still for about a second.</summary>
        internal static void OnEvaluatorInit(SearchPathRequest.BranchEvaluator ev, Func<XYZ, bool> presenceTest, bool canClimb, bool canBreakVoxels)
        {
            bool phantom = false;
            if (BtcPhantom.Ghost && BtcPhantom.LiveCount > 0 && presenceTest != null)
            {
                object t = presenceTest.Target;
                SearchPathRequest r = t as SearchPathRequest;
                if (r != null) phantom = r.Navigation != null && BtcPhantomGhost.On(r.Navigation.gameObject);
                else
                {
                    DestinationRepathSearch d = t as DestinationRepathSearch;
                    phantom = d != null && BtcPhantomGhost.On(FRepathGo(d));
                }
            }
            if (!phantom) { if (ForestEval.Count > 0) ForestEval.Remove(ev); return; }
            ev.AcceptedType = canBreakVoxels ? PathBranch.BranchType.Breaking : (canClimb ? PathBranch.BranchType.Climbing : PathBranch.BranchType.StepDown);
            ForestEval.Add(ev);
        }

        internal static void Reset() { ForestEval.Clear(); }
    }
}
