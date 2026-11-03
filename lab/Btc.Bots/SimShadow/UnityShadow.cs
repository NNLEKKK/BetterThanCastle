// Shadow of the UnityEngine 5.6 types the mod's duel bot sources use. Semantics copied from the Unity 5.6 managed sources
// (UnityEngine.Vector3 / Mathf / Random). Only what the bots and the host need; everything is internal to Btc.Bots.
using System;
using Btc.Bots;
using Btc.Sim;

namespace UnityEngine
{
    internal struct Vector3 : IEquatable<Vector3>
    {
        public const float kEpsilon = 1E-05f;
        public float x, y, z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }

        public float this[int index]
        {
            get { switch (index) { case 0: return x; case 1: return y; case 2: return z; default: throw new IndexOutOfRangeException("Invalid Vector3 index!"); } }
            set { switch (index) { case 0: x = value; break; case 1: y = value; break; case 2: z = value; break; default: throw new IndexOutOfRangeException("Invalid Vector3 index!"); } }
        }

        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } }
        public static Vector3 forward { get { return new Vector3(0f, 0f, 1f); } }
        public static Vector3 back { get { return new Vector3(0f, 0f, -1f); } }
        public static Vector3 up { get { return new Vector3(0f, 1f, 0f); } }
        public static Vector3 down { get { return new Vector3(0f, -1f, 0f); } }
        public static Vector3 left { get { return new Vector3(-1f, 0f, 0f); } }
        public static Vector3 right { get { return new Vector3(1f, 0f, 0f); } }

        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { return Normalize(this); } }

        public void Set(float nx, float ny, float nz) { x = nx; y = ny; z = nz; }
        public void Normalize()
        {
            float m = Magnitude(this);
            if (m > 1E-05f) this = this / m; else this = zero;
        }
        public static Vector3 Normalize(Vector3 value)
        {
            float m = Magnitude(value);
            if (m > 1E-05f) return value / m;
            return zero;
        }
        public static float Magnitude(Vector3 a) { return Mathf.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z); }
        public static float SqrMagnitude(Vector3 a) { return a.x * a.x + a.y * a.y + a.z * a.z; }
        public static float Distance(Vector3 a, Vector3 b)
        {
            Vector3 v = new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
            return Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }
        public static float Dot(Vector3 l, Vector3 r) { return l.x * r.x + l.y * r.y + l.z * r.z; }
        public static Vector3 Cross(Vector3 l, Vector3 r) { return new Vector3(l.y * r.z - l.z * r.y, l.z * r.x - l.x * r.z, l.x * r.y - l.y * r.x); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 a = target - current;
            float m = a.magnitude;
            if (m <= maxDistanceDelta || m == 0f) return target;
            return current + a / m * maxDistanceDelta;
        }
        public static Vector3 ClampMagnitude(Vector3 v, float maxLength)
        {
            if (v.sqrMagnitude > maxLength * maxLength) return v.normalized * maxLength;
            return v;
        }
        public static float Angle(Vector3 from, Vector3 to) { return Mathf.Acos(Mathf.Clamp(Dot(from.normalized, to.normalized), -1f, 1f)) * 57.29578f; }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z)); }
        public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)); }
        public static Vector3 Project(Vector3 v, Vector3 n)
        {
            float d = Dot(n, n);
            if (d < Mathf.Epsilon) return zero;
            return n * Dot(v, n) / d;
        }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { return v - Project(v, n); }

        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        public static bool operator ==(Vector3 l, Vector3 r) { return SqrMagnitude(l - r) < 9.99999944E-11f; }
        public static bool operator !=(Vector3 l, Vector3 r) { return !(l == r); }

        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2; }
        public override bool Equals(object other) { return other is Vector3 && Equals((Vector3)other); }
        public bool Equals(Vector3 o) { return x.Equals(o.x) && y.Equals(o.y) && z.Equals(o.z); }
        public override string ToString() { return "(" + x.ToString("F1") + ", " + y.ToString("F1") + ", " + z.ToString("F1") + ")"; }
        public string ToString(string format) { return "(" + x.ToString(format) + ", " + y.ToString(format) + ", " + z.ToString(format) + ")"; }
    }

    internal struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0f, 0f); } }
        public static Vector2 one { get { return new Vector2(1f, 1f); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { Vector2 r = new Vector2(x, y); r.Normalize(); return r; } }
        public void Normalize() { float m = magnitude; if (m > 1E-05f) this = this / m; else this = zero; }
        public static float Dot(Vector2 l, Vector2 r) { return l.x * r.x + l.y * r.y; }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static bool operator ==(Vector2 l, Vector2 r) { return (l - r).sqrMagnitude < 9.99999944E-11f; }
        public static bool operator !=(Vector2 l, Vector2 r) { return !(l == r); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0f); }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode() << 2; }
        public override bool Equals(object other) { return other is Vector2 && Equals((Vector2)other); }
        public bool Equals(Vector2 o) { return x.Equals(o.x) && y.Equals(o.y); }
        public override string ToString() { return "(" + x.ToString("F1") + ", " + y.ToString("F1") + ")"; }
    }

    /// <summary>Unity 5.6 Mathf: float wrappers over System.Math (double), same rounding (Math.Round = banker's).</summary>
    internal static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public static readonly float Epsilon = 1.401298E-45f;   // Unity on x86/x64 (no flush-to-zero): float.Epsilon

        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Tan(float f) { return (float)Math.Tan(f); }
        public static float Asin(float f) { return (float)Math.Asin(f); }
        public static float Acos(float f) { return (float)Math.Acos(f); }
        public static float Atan(float f) { return (float)Math.Atan(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Min(float a, float b) { return a >= b ? b : a; }
        public static float Min(params float[] values)
        {
            int n = values.Length; if (n == 0) return 0f;
            float r = values[0]; for (int i = 1; i < n; i++) if (values[i] < r) r = values[i];
            return r;
        }
        public static int Min(int a, int b) { return a >= b ? b : a; }
        public static int Min(params int[] values)
        {
            int n = values.Length; if (n == 0) return 0;
            int r = values[0]; for (int i = 1; i < n; i++) if (values[i] < r) r = values[i];
            return r;
        }
        public static float Max(float a, float b) { return a <= b ? b : a; }
        public static float Max(params float[] values)
        {
            int n = values.Length; if (n == 0) return 0f;
            float r = values[0]; for (int i = 1; i < n; i++) if (values[i] > r) r = values[i];
            return r;
        }
        public static int Max(int a, int b) { return a <= b ? b : a; }
        public static int Max(params int[] values)
        {
            int n = values.Length; if (n == 0) return 0;
            int r = values[0]; for (int i = 1; i < n; i++) if (values[i] > r) r = values[i];
            return r;
        }
        public static float Pow(float f, float p) { return (float)Math.Pow(f, p); }
        public static float Exp(float p) { return (float)Math.Exp(p); }
        public static float Log(float f) { return (float)Math.Log(f); }
        public static float Log10(float f) { return (float)Math.Log10(f); }
        public static float Ceil(float f) { return (float)Math.Ceiling(f); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Round(float f) { return (float)Math.Round(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static float Sign(float f) { return f < 0f ? -1f : 1f; }
        public static float Clamp(float value, float min, float max) { if (value < min) value = min; else if (value > max) value = max; return value; }
        public static int Clamp(int value, int min, int max) { if (value < min) value = min; else if (value > max) value = max; return value; }
        public static float Clamp01(float value) { if (value < 0f) return 0f; if (value > 1f) return 1f; return value; }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float value) { return a != b ? Clamp01((value - a) / (b - a)) : 0f; }
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }
        public static bool Approximately(float a, float b) { return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f); }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Abs(t - length); }
        public static float DeltaAngle(float current, float target) { float n = Repeat(target - current, 360f); if (n > 180f) n -= 360f; return n; }
    }

    /// <summary>Clock of the ACTIVE match (SimContext.Cur, per thread). Unity semantics: time = frame time in Update, fixedTime inside FixedUpdate.</summary>
    internal static class Time
    {
        public static float time { get { SimContext c = SimContext.Cur; return c != null ? (float)c.Time : 0f; } }
        public static float deltaTime { get { SimContext c = SimContext.Cur; return c != null ? c.DeltaTime : 0f; } }
        public static float smoothDeltaTime { get { return deltaTime; } }
        public static float unscaledDeltaTime { get { return deltaTime; } }
        public static float fixedTime { get { SimContext c = SimContext.Cur; return c != null ? (float)c.FixedTime : 0f; } }
        public static float fixedDeltaTime { get { return DuelSim.FixedDt; } }
        public static int frameCount { get { SimContext c = SimContext.Cur; return c != null ? c.FrameCount : 0; } }
        public static float realtimeSinceStartup { get { return time; } }
        public static float timeScale { get { return 1f; } }
    }

    /// <summary>Unity-like Random (xorshift128, value inclusive [0,1], int Range max exclusive). State lives in the active match context.</summary>
    internal static class Random
    {
        public static float value { get { return Float01(); } }
        private static float Float01() { return ((SimContext.Cur ?? SimContext.Fallback).NextUInt() & 0x007FFFFFu) * (1f / 8388607f); }
        public static float Range(float min, float max) { float t = Float01(); return t * min + (1f - t) * max; }
        public static int Range(int min, int max)
        {
            SimContext c = SimContext.Cur ?? SimContext.Fallback;
            if (min < max) return min + (int)(c.NextUInt() % (uint)(max - min));
            if (min > max) return min - (int)(c.NextUInt() % (uint)(min - max));
            return min;
        }
        public static void InitState(int seed) { (SimContext.Cur ?? SimContext.Fallback).Seed((uint)seed); }
    }

    /// <summary>Opaque unit handle. InstanceID = sim unit index + 1000. Unit = the duel unit it belongs to (fast BtcDuel.TryGet).</summary>
    internal sealed class GameObject
    {
        public string name;
        internal readonly int InstanceId;
        internal readonly int Index;
        internal BetterThanCastle.BtcDuelUnitRef Unit;
        internal Transform Tf;
        public GameObject(string name, int index) { this.name = name; Index = index; InstanceId = index + 1000; }
        public int GetInstanceID() { return InstanceId; }
        public Transform transform { get { return Tf; } }
        public GameObject gameObject { get { return this; } }
        public override string ToString() { return name; }
    }

    /// <summary>Reads the sim arrays on every access (same as a live Unity transform). y = arena floor.</summary>
    internal sealed class Transform
    {
        internal DuelSim Sim;
        internal int I;
        internal float Y;
        internal GameObject Go;
        public Vector3 position
        {
            get { return new Vector3(Sim.X[I], Y, Sim.Z[I]); }
            set { Sim.X[I] = value.x; Sim.Z[I] = value.z; }
        }
        public Vector3 forward { get { return I < Sim.FX.Length ? new Vector3(Sim.FX[I], 0f, Sim.FZ[I]) : new Vector3(0f, 0f, 1f); } }
        public GameObject gameObject { get { return Go; } }
    }

    internal static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
    }
}
