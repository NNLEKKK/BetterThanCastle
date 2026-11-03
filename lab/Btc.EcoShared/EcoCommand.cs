// See EcoCmdEnum.cs for the format description. net35-compatible.
using System;
using System.Globalization;
using System.Text;

namespace Btc.EcoShared
{
    public struct EcoCommand
    {
        public EcoCmd Cmd;
        public int Actor;     // netId of the acting unit/building/project (0 = none)
        public int Target;    // netId of the target object (0 = none)
        public int X, Y, Z;   // voxel (world coords)
        public int P0, P1, P2, P3;
        public float F0, F1;

        public override string ToString() { return EcoCommandText.Format(this); }
    }

    public static class EcoCommandText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Format(EcoCommand c)
        {
            StringBuilder sb = new StringBuilder(64);
            sb.Append(EcoCmdNames.Names[(int)c.Cmd]);
            sb.Append('|').Append(c.Actor.ToString(Inv));
            sb.Append('|').Append(c.Target.ToString(Inv));
            sb.Append('|').Append(c.X.ToString(Inv)).Append(',').Append(c.Y.ToString(Inv)).Append(',').Append(c.Z.ToString(Inv));
            sb.Append('|').Append(c.P0.ToString(Inv)).Append(',').Append(c.P1.ToString(Inv)).Append(',').Append(c.P2.ToString(Inv)).Append(',').Append(c.P3.ToString(Inv));
            sb.Append('|').Append(c.F0.ToString("0.000", Inv)).Append(',').Append(c.F1.ToString("0.000", Inv));
            return sb.ToString();
        }

        /// <summary>Full trace line "t|CMD|Name|..." (t in seconds).</summary>
        public static string FormatLine(float t, EcoCommand c)
        {
            return t.ToString("0.000", Inv) + "|CMD|" + Format(c);
        }

        /// <summary>Parses "Name|actor|target|x,y,z|p0..p3|f0,f1" (body after "CMD|").</summary>
        public static bool TryParseBody(string body, out EcoCommand c)
        {
            c = default(EcoCommand);
            if (body == null) return false;
            string[] f = body.Split('|');
            EcoCmd cmd;
            if (f.Length < 1 || !EcoCmdNames.TryParse(f[0].Trim(), out cmd)) return false;
            c.Cmd = cmd;
            int v;
            if (f.Length > 1 && f[1].Length > 0) { if (!int.TryParse(f[1], NumberStyles.Integer, Inv, out v)) return false; c.Actor = v; }
            if (f.Length > 2 && f[2].Length > 0) { if (!int.TryParse(f[2], NumberStyles.Integer, Inv, out v)) return false; c.Target = v; }
            if (f.Length > 3 && f[3].Length > 0)
            {
                int[] a = new int[3];
                if (!ParseInts(f[3], a)) return false;
                c.X = a[0]; c.Y = a[1]; c.Z = a[2];
            }
            if (f.Length > 4 && f[4].Length > 0)
            {
                int[] a = new int[4];
                if (!ParseInts(f[4], a)) return false;
                c.P0 = a[0]; c.P1 = a[1]; c.P2 = a[2]; c.P3 = a[3];
            }
            if (f.Length > 5 && f[5].Length > 0)
            {
                string[] s = f[5].Split(',');
                float x;
                if (s.Length > 0 && s[0].Length > 0) { if (!float.TryParse(s[0], NumberStyles.Float, Inv, out x)) return false; c.F0 = x; }
                if (s.Length > 1 && s[1].Length > 0) { if (!float.TryParse(s[1], NumberStyles.Float, Inv, out x)) return false; c.F1 = x; }
            }
            return true;
        }

        static bool ParseInts(string s, int[] dst)
        {
            string[] p = s.Split(',');
            for (int i = 0; i < p.Length && i < dst.Length; i++)
            {
                if (p[i].Length == 0) continue;
                if (!int.TryParse(p[i], NumberStyles.Integer, Inv, out dst[i])) return false;
            }
            return true;
        }

        /// <summary>Parses a whole trace line "t|CMD|body". Returns false for non-CMD lines (not an error).</summary>
        public static bool TryParseLine(string line, out float t, out EcoCommand c)
        {
            t = 0; c = default(EcoCommand);
            if (line == null) return false;
            int a = line.IndexOf('|');
            if (a <= 0) return false;
            int b = line.IndexOf('|', a + 1);
            if (b < 0 || b - a - 1 != 3 || string.CompareOrdinal(line, a + 1, "CMD", 0, 3) != 0) return false;
            if (!float.TryParse(line.Substring(0, a), NumberStyles.Float, Inv, out t)) return false;
            return TryParseBody(line.Substring(b + 1), out c);
        }
    }
}
