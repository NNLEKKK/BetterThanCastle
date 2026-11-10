using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    internal enum VKind { Bool, Float, Color, Choice }

    /// <summary>One declarative setting of the colored-HP / visuals config. The bridge (ResourcePackManager.Visual.cs) fills these with lambdas over the
    /// private HpConfig; the Visuals tab draws, searches, undoes and exports (profiles) them without knowing HpConfig.</summary>
    internal sealed class BtcVisualSetting
    {
        public string Key, Page, Section, En, Ru;
        public VKind Kind;
        public bool Expert;
        public int Indent;
        public Func<bool> Vis;
        public Func<bool> GetB; public Action<bool> SetB;
        public Func<float> GetF; public Action<float> SetF; public float Min, Max, Step; public string Fmt = "0", Suf = "";
        public Func<Color> GetC; public Action<Color> SetC;
        public Func<int> GetI; public Action<int> SetI; public string[] Choices;
        public string Label { get { return BtcReplayUI.T(En, Ru); } }

        internal string Export()
        {
            switch (Kind)
            {
                case VKind.Bool: return GetB() ? "1" : "0";
                case VKind.Float: return GetF().ToString("R", CultureInfo.InvariantCulture);
                case VKind.Color: return BtcVisualColor.Hex(GetC());
                default: return GetI().ToString(CultureInfo.InvariantCulture);
            }
        }

        internal void Import(string v)
        {
            switch (Kind)
            {
                case VKind.Bool: SetB(v == "1"); break;
                case VKind.Float: { float f; if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) SetF(Mathf.Clamp(f, Min, Max)); break; }
                case VKind.Color: { Color c; if (BtcVisualColor.TryHex(v, out c)) SetC(c); break; }
                default: { int i; if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) SetI(Mathf.Clamp(i, 0, Choices.Length - 1)); break; }
            }
        }
    }

    /// <summary>A palette = list of colour stops (percent + colour) with an optional "2nd colour brightness".</summary>
    internal sealed class BtcVisualPalette
    {
        public string Key, En, Ru;
        public Func<bool> Vis;
        public Func<int> Count;
        public Func<int, float> Pct; public Action<int, float> SetPct;
        public Func<int, Color> Col; public Action<int, Color> SetCol;
        public Action Add; public Action<int> Remove;
        public Func<float> Second; public Action<float> SetSecond;
        public Func<object> Snap; public Action<object> Restore;
        public string Label { get { return BtcReplayUI.T(En, Ru); } }
    }

    internal static class BtcVisualData
    {
        internal static readonly List<BtcVisualSetting> All = new List<BtcVisualSetting>();
        internal static readonly List<BtcVisualPalette> Palettes = new List<BtcVisualPalette>();
        internal static Func<bool> Ready;
        internal static Action Reset;
        internal static Func<bool> GetMaster; internal static Action<bool> SetMaster;
        internal static Func<bool> GetDiscord; internal static Action<bool> SetDiscord;
        // shader overrides (team 0 own, 1 enemy; index into OvNames)
        internal static string[] OvNames;
        internal static Func<int, bool> OvIsColor;
        internal static Func<int, int, bool> OvOn, OvMult;
        internal static Action<int, int> OvToggle;
        internal static Func<int, int, float> OvF;
        internal static Action<int, int, float> OvSetF;
        internal static Func<int, int, Color> OvC;
        internal static Action<int, int, Color> OvSetC;
        internal static Func<bool> ResetArmed; internal static Action ArmReset;
        internal static Func<bool> GetAutoGamma; internal static Action<bool> SetAutoGamma;
        private static Dictionary<string, BtcVisualSetting> _map;

        internal static BtcVisualSetting Find(string key)
        {
            if (_map == null || _map.Count != All.Count)
            {
                _map = new Dictionary<string, BtcVisualSetting>();
                for (int i = 0; i < All.Count; i++) _map[All[i].Key] = All[i];
            }
            BtcVisualSetting s;
            return _map.TryGetValue(key, out s) ? s : null;
        }
    }
}

namespace BetterThanCastle
{
    /// <summary>Copy of a palette (for undo).</summary>
    internal sealed class BtcPalSnap { public float[] Pct; public Color[] Col; public float Second; }
}
