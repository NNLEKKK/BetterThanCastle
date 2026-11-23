namespace BetterThanCastle
{
    /// Frame savers: CPU work the game repeated every frame for the same answer, done once. Ported from Castle Story Plus
    /// by Tricky12321 (GPLv3, CastleStoryPlus/Performance). Every saver gives the same result as the game (same values,
    /// same signals), so it is local and needs no BetterConnection gate: nothing changes on the wire or in the simulation.
    /// Config Performance/FrameSavers, read once at start: each patch class has Prepare() => BtcFrameSave.On.
    internal static class BtcFrameSave
    {
        private static bool? _on;

        /// Snapshot at plugin start: patches are applied only if it was on then (changing it needs a restart).
        internal static bool On
        {
            get
            {
                if (!_on.HasValue) _on = BtcConfig.FrameSavers == null || BtcConfig.FrameSavers.Value;
#if DEV
                string ab = System.Environment.GetEnvironmentVariable("BTC_FRAMESAVERS"); // A/B runs: 0 = off, 1 = on
                if (ab == "0") _on = false; else if (ab == "1") _on = true;
#endif
                return _on.Value;
            }
        }
    }
}
