using System;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game.Components.Volumes;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantoms walk through objects (rule phantom.ghost, host only, off while phantom.fly flies them): the vanilla path search and walk,
    /// but every obstacle volume that is not made of blocks (buildings incl. enemy gates and doors, any placed object) is ignored,
    /// enemy bricktrons do not block (own ones do: the vanilla aggressive presence test), and the forest is walkable (vanilla
    /// "walk in forest"). Terrain, bricks, beams (voxels and block volumes) stay solid; tree trunks, boulders, iron and pyrite
    /// (nature blockers) are no
    /// walk-graph nodes at all, so they are still walked around (user decision 2026-11-16, the shared graph is not touched).
    /// </summary>
    internal static class BtcPhantomGhost
    {
        /// <summary>The unit is a live phantom that walks through objects.</summary>
        internal static bool On(GameObject go) { return BtcPhantom.Ghost && go != null && BtcPhantom.IsPhantom(go); }

        /// <summary>An obstacle volume that still stops a ghost phantom: made of blocks (multi-block cells, block volumes without an
        /// object). Passed: anything with a GameObject (buildings, placed objects), boulders and minerals (nature FreeVolumes), gates.</summary>
        private static bool Solid(IVolume v)
        {
            if (v.GameObject != null || v.PassThrough is Brix.Legacy.Porte) return false;
            FreeVolume f = v as FreeVolume;
            if (f != null)
            {
                object k = f.AssetKey;
                if (k != null && (k.Equals(NatureVolume.SmallMineral) || k.Equals(NatureVolume.MediumMineral) || k.Equals(NatureVolume.BigMineral))) return false;
            }
            return true;
        }

        // only block-made obstacles stop a ghost phantom
        private static readonly Func<IVolume, GameObject, bool> SearchBlock = (v, g) => Solid(v) && !v.PassThrough.CanSearchThrough(g);
        private static readonly Func<IVolume, GameObject, bool> PassBlock = (v, g) => Solid(v) && v.GameObject != g && !v.PassThrough.CanPassThrough(g);
        private static readonly Func<IVolume, GameObject, bool> PassAny = (v, g) => v.GameObject != g && !v.PassThrough.CanPassThrough(g);

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
    }
}
