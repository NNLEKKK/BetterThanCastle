namespace BetterThanCastle
{
    /// <summary>Replaces the compiler-generated &lt;PrivateImplementationDetails&gt;.ComputeStringHash that dnSpy exposes
    /// for "switch (string)" decompiled as hash trees. Same FNV-1a variant the C# compiler uses.</summary>
    internal static class BtcStr
    {
        /// <summary>Nick without rich-text tags (colour tags etc.).</summary>
        internal static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s ?? string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", string.Empty).Trim();
        }

        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string[]>> Keys =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string[]>>();

        /// prefix + n + suffix, built once and cached (per-frame bind lookups like "camera_slot_save_" + i made garbage every frame).
        internal static string Key(string prefix, int n, string suffix = "")
        {
            System.Collections.Generic.Dictionary<string, string[]> bySuffix;
            if (!Keys.TryGetValue(prefix, out bySuffix)) { bySuffix = new System.Collections.Generic.Dictionary<string, string[]>(); Keys[prefix] = bySuffix; }
            string[] arr;
            if (!bySuffix.TryGetValue(suffix, out arr) || n >= arr.Length)
            {
                if (n < 0 || n > 4096) return prefix + n + suffix;
                string[] grown = new string[System.Math.Max(n + 1, 16)];
                if (arr != null) System.Array.Copy(arr, grown, arr.Length);
                arr = grown; bySuffix[suffix] = arr;
            }
            return arr[n] ?? (arr[n] = prefix + n + suffix);
        }

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
