namespace BetterThanCastle
{
    /// <summary>
    /// Every network-touching patch must check <see cref="Enabled"/> at call time and fall through
    /// to vanilla behaviour when it is false. Never cache the value.
    /// </summary>
    internal static class BtcNetworkGate
    {
        internal static bool Enabled
        {
            get { return BtcConfig.BetterConnection == null || BtcConfig.BetterConnection.Value; }
        }
    }
}
