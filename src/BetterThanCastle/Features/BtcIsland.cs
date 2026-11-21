using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Map editor brush extras: square brush shape (all editor brushes) and a sloped Island: dragging a line raises/lowers the
    /// target height in proportion to the distance from where the stroke started (smooth ramps, no stepping on the voxel grid).
    /// State is remembered through BtcMenuState. Patches/IslandPatches.cs applies it.
    /// </summary>
    internal static class BtcIsland
    {
        private static int _sq = -1;
        private static int _slope = int.MinValue;

        internal static bool Square
        {
            get { if (_sq < 0) _sq = BtcMenuState.Bool("island:square", false) ? 1 : 0; return _sq == 1; }
            set { _sq = value ? 1 : 0; BtcMenuState.SetBool("island:square", value); }
        }

        /// <summary>Height change per 100 horizontal voxels (percent), negative = going down. 0 = vanilla.</summary>
        internal static int Slope
        {
            get { if (_slope == int.MinValue) _slope = BtcMenuState.Int("island:slope", 0); return _slope; }
            set { _slope = Mathf.Clamp(value, -200, 200); BtcMenuState.SetInt("island:slope", _slope); }
        }

        private static float _lastCall = -10f;
        private static Vector3 _start;

        /// <summary>Returns the hit point with the slope applied (same hit when the slope is 0).</summary>
        internal static Vector3 Sloped(Vector3 hit)
        {
            int s = Slope;
            float now = Time.unscaledTime;
            bool fresh = now - _lastCall > 0.4f;
            _lastCall = now;
            if (s == 0) return hit;
            if (fresh) _start = hit;
            float dx = hit.x - _start.x, dz = hit.z - _start.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            return new Vector3(hit.x, _start.y + dist * s * 0.01f, hit.z);
        }
    }
}
