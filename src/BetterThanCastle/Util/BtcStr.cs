namespace BetterThanCastle
{
    /// <summary>Replaces the compiler-generated &lt;PrivateImplementationDetails&gt;.ComputeStringHash that dnSpy exposes
    /// for "switch (string)" decompiled as hash trees. Same FNV-1a variant the C# compiler uses.</summary>
    internal static class BtcStr
    {
        internal static uint ComputeStringHash(string s)
        {
            uint h = 2166136261u;
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    h = (s[i] ^ h) * 16777619u;
                }
            }
            return h;
        }
    }
}
