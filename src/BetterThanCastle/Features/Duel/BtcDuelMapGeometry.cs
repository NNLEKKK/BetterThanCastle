using UnityEngine;

namespace BetterThanCastle
{
    internal enum BtcDuelZone { Out = 0, SafeA, NeutralA, Mid, NeutralB, SafeB }

    /// <summary>Pure arena geometry (zones, spawn slots). No engine calls: this file is also compiled into the lab simulator.</summary>
    internal static partial class BtcDuelMap
    {
        // zone layout along +X; everything scales linearly with Scale (1 = 100x20, 3 = 300x60)
#if SIM
        [System.ThreadStatic]
#endif
        internal static int Scale = 1;
#if SIM
        [System.ThreadStatic]
#endif
        internal static int Len = 100, Wid = 20;
#if SIM
        [System.ThreadStatic]
#endif
        internal static int SafeLen = 15, NeutralLen = 20, MidLen = 30;
#if SIM
        [System.ThreadStatic]
#endif
        private static int _pX0, _pZ0, _pLen, _pWid, _pTop;   // previous arena (rebuild with another size keeps the centre)
#if SIM
        [System.ThreadStatic]
#endif
        private static float _cx, _cz;

        internal static void SetScale(int s)
        {
            if (Built) { _pX0 = X0; _pZ0 = Z0; _pLen = Len; _pWid = Wid; _pTop = Top; }
            Scale = s < 1 ? 1 : s;
            Len = 100 * Scale; Wid = 20 * Scale; SafeLen = 15 * Scale; NeutralLen = 20 * Scale; MidLen = 30 * Scale;
        }
#if SIM
        [System.ThreadStatic]
#endif
        internal static int X0, Z0, Top; // voxel origin (west-south corner) and surface voxel index
#if SIM
        [System.ThreadStatic]
#endif
        internal static bool Built;

        internal static float MidX0 { get { return X0 + SafeLen + NeutralLen; } }
        internal static float MidX1 { get { return X0 + SafeLen + NeutralLen + MidLen; } }
        internal static float CenterX { get { return X0 + Len * 0.5f; } }
        internal static float CenterZ { get { return Z0 + Wid * 0.5f; } }
        /// <summary>Surface height in world units (units stand on this).</summary>
        internal static float Floor { get { return Top + 1f; } }

        internal static BtcDuelZone ZoneAt(float x, float z)
        {
            float rx = x - X0, rz = z - Z0;
            if (rx < 0f || rx >= Len || rz < 0f || rz >= Wid) return BtcDuelZone.Out;
            if (rx < SafeLen) return BtcDuelZone.SafeA;
            if (rx < SafeLen + NeutralLen) return BtcDuelZone.NeutralA;
            if (rx < SafeLen + NeutralLen + MidLen) return BtcDuelZone.Mid;
            if (rx < Len - SafeLen) return BtcDuelZone.NeutralB;
            return BtcDuelZone.SafeB;
        }

        /// <summary>Spawn point for slot i of n of a side (a block of rows inside the side's safe zone, facing the middle).</summary>
        internal static Vector3 SpawnPoint(int side, int i, int n)
        {
            int rows = Mathf.Max(1, Mathf.Min(n, Wid - 4));
            int col = i / rows, row = i % rows;
            int inCol = Mathf.Min(rows, n - col * rows);
            float x = side == 0 ? X0 + 2.5f + col * 1.5f : X0 + Len - 2.5f - col * 1.5f;
            float z = Z0 + Wid * 0.5f + (row - (inCol - 1) * 0.5f) * 1.2f;
            return new Vector3(x, Floor + 0.08f, z);   // just above the surface: a higher drop puts the unit into the landing stun and it ignores orders
        }
    }
}
