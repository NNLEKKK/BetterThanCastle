using System;
using System.Collections.Generic;
using BetterThanCastle;

namespace Btc.Bots
{
    /// <summary>
    /// Everything the shadows (Time, Random, BtcDuel, BtcDuelConfig, BtcDuelLog, BtcLog) read: one instance per match, made current per thread
    /// by SimBotHost.Activate. Several matches may interleave on one thread; each Activate swaps the whole context.
    /// </summary>
    internal sealed class SimContext
    {
        [ThreadStatic] internal static SimContext Cur;
        [ThreadStatic] private static SimContext _fallback;
        /// <summary>Used by Random when no match is active (tests of the shadow itself).</summary>
        internal static SimContext Fallback { get { return _fallback ?? (_fallback = new SimContext()); } }

        // clock (Unity Time)
        internal double Time, FixedTime;
        internal float DeltaTime;
        internal int FrameCount;

        // Random (xorshift128, Unity's seeding)
        private uint _x, _y, _z, _w;
        internal SimContext() { Seed(0x2545F491u); }
        internal void Seed(uint seed)
        {
            _x = seed; _y = _x * 1812433253u + 1u; _z = _y * 1812433253u + 1u; _w = _z * 1812433253u + 1u;
        }
        internal uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }

        // BtcDuel statics
        internal SimBotHost Host;
        internal BtcDuelMatch Match;
        internal readonly Dictionary<int, BtcDuelUnitRef> ById = new Dictionary<int, BtcDuelUnitRef>();
        internal readonly bool[] GuardOrders = new bool[2];
        internal bool SkipAnims = true;
        internal int AutoPlayerLevel = -1, AutoPlayerKind, AutoPlayerTier = 4;
        internal float LabDodgeMs;

        // BtcDuelConfig
        internal int[] PlayerCount = new int[8], BotCount = new int[8];
        internal BtcDuelDifficulty Difficulty = BtcDuelDifficulty.Chosen;
        internal bool InfiniteHeal;
        internal int ArenaScale = 1;

        // logging
        internal Action<float, string, string> LogSink;   // (time since fight start, tag, msg); null = off
        internal float LogT0;
        internal bool BotOrders = true;
        internal int Errors;
        internal string LastError;
    }
}
