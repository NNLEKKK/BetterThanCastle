// Shadows of the mod-side types the linked bot sources use (BtcDuel, BtcDuelConfig, BtcDuelLog, BtcLog, BtcDuelMatch).
// Order functions and the Guarded rule are copied from src/BetterThanCastle/Features/Duel/BtcDuel.cs; the vanilla Order.*.Send call is
// replaced by SimBotHost.IssueOrder (which also does what the Labor.SendOrder hook -> BtcDuelMatch.OnOrder does).
using System;
using System.Collections.Generic;
using Btc.Bots;
using Btc.Sim;
using UnityEngine;

namespace BetterThanCastle
{
    internal enum BtcDuelUnit { Worker = 0, BarrelWorker, Knight, Archer, Halberdier, Arbalist, Alchemist, Geomancer }

    internal enum BtcDuelDifficulty { Easy = 0, Medium, Hard, Extreme, Chosen, Custom }

    internal static class BtcDuel
    {
        internal static bool Active { get { return SimContext.Cur != null; } }
        internal static BtcDuelMatch Match { get { return SimContext.Cur.Match; } }
        internal static bool SkipAnims { get { return SimContext.Cur.SkipAnims; } set { SimContext.Cur.SkipAnims = value; } }
        internal static int AutoPlayerLevel { get { return SimContext.Cur.AutoPlayerLevel; } set { SimContext.Cur.AutoPlayerLevel = value; } }
        internal static int AutoPlayerKind { get { return SimContext.Cur.AutoPlayerKind; } set { SimContext.Cur.AutoPlayerKind = value; } }
        internal static int AutoPlayerTier { get { return SimContext.Cur.AutoPlayerTier; } set { SimContext.Cur.AutoPlayerTier = value; } }
        internal static float LabDodgeMs { get { return SimContext.Cur.LabDodgeMs; } set { SimContext.Cur.LabDodgeMs = value; } }
        internal const float VanillaCycleMs = 1500f;
        internal static Dictionary<int, BtcDuelUnitRef> ById { get { return SimContext.Cur.ById; } }

        /// <summary>Same result as the mod's ById lookup (the host keeps GameObject.Unit and ById in sync); no dictionary hit.</summary>
        internal static bool TryGet(GameObject go, out BtcDuelUnitRef u)
        {
            u = null;
            if (go == null) return false;
            u = go.Unit;
            return u != null;
        }

        internal static bool[] GuardOrders { get { return SimContext.Cur.GuardOrders; } }
        internal const float StrikeWindow = 0.56f;

        private static bool Guarded(BtcDuelUnitRef u, bool force)
        {
            if (force || !GuardOrders[u.Side]) return false;
            if (Time.time - u.StrikeAt >= StrikeWindow) return false;
            u.TargetId = -9;                 // the caller thinks the order went out: make it re-issue next tick
            return true;
        }

        internal static void MoveAttack(BtcDuelUnitRef u, Vector3 pos, bool force = false)
        {
            if (Guarded(u, force)) return;
            u.LastOrderAt = Time.time; u.LastDest = pos; u.PathDest = Vector3.zero;
            if (BtcDuelLog.BotOrders) BtcDuelLog.Line("BORD", "u" + u.Id + " MoveAttack to=" + pos.x.ToString("0.0") + "," + pos.z.ToString("0.0"));
            SimContext.Cur.Host.IssueOrder(u.Go.Index, SimOrder.MoveAttack, pos.x, pos.z, -1);
        }

        internal static void Attack(BtcDuelUnitRef u, GameObject target, bool force = false)
        {
            if (Guarded(u, force)) return;
            u.LastOrderAt = Time.time; u.PathDest = Vector3.zero;
            if (BtcDuelLog.BotOrders) { BtcDuelUnitRef bt; BtcDuel.TryGet(target, out bt); BtcDuelLog.Line("BORD", "u" + u.Id + " Attack >" + (bt != null ? "u" + bt.Id : "?")); }
            if (target == null) { BtcLog.Error("Duel", "Attack order", new NullReferenceException("target")); return; }   // vanilla Send throws on a null target, caught by the mod
            SimContext.Cur.Host.IssueOrder(u.Go.Index, SimOrder.Attack, 0f, 0f, target.Index);
        }

        internal static void Move(BtcDuelUnitRef u, Vector3 pos, bool force = false)
        {
            if (Guarded(u, force)) return;
            u.LastOrderAt = Time.time; u.LastDest = pos; u.PathDest = Vector3.zero;
            if (BtcDuelLog.BotOrders) BtcDuelLog.Line("BORD", "u" + u.Id + " Move to=" + pos.x.ToString("0.0") + "," + pos.z.ToString("0.0"));
            SimContext.Cur.Host.IssueOrder(u.Go.Index, SimOrder.Move, pos.x, pos.z, -1);
        }
    }

    internal static class BtcDuelConfig
    {
        internal static readonly int UnitKinds = 8;
        internal static readonly string[] UnitNames = { "Worker", "Barrel worker", "Knight", "Archer", "Halberdier", "Arbalist", "Alchemist", "Geomancer" };
        internal static int[] PlayerCount { get { return SimContext.Cur.PlayerCount; } }
        internal static int[] BotCount { get { return SimContext.Cur.BotCount; } }
        internal static BtcDuelDifficulty Difficulty { get { return SimContext.Cur.Difficulty; } }
        internal static bool SkipAnims { get { return SimContext.Cur.SkipAnims; } }
        internal static bool InfiniteHeal { get { return SimContext.Cur.InfiniteHeal; } }
        internal static int ArenaScale { get { return SimContext.Cur.ArenaScale; } }
        internal static int Total(int[] c) { int n = 0; for (int i = 0; i < c.Length; i++) n += c[i]; return n; }
        internal static void Save() { }
    }

    /// <summary>Match log: forwards to SimContext.LogSink when set (off by default). Callers still build their strings (as in the game).</summary>
    internal static class BtcDuelLog
    {
        internal static string CurrentPath { get { return null; } }
        internal static bool Open { get { SimContext c = SimContext.Cur; return c != null && c.LogSink != null; } }
        internal static bool BotOrders { get { SimContext c = SimContext.Cur; return c != null && c.BotOrders && c.LogSink != null; } }
        internal static void Begin() { }
        internal static void SetT0() { SimContext c = SimContext.Cur; if (c != null) c.LogT0 = (float)c.Time; }
        internal static void End() { }
        internal static void Line(string tag, string msg)
        {
            SimContext c = SimContext.Cur;
            if (c == null || c.LogSink == null) return;
            c.LogSink((float)c.Time - c.LogT0, tag, msg);
        }
    }

    internal static class BtcLog
    {
        internal static void Info(string tag, string msg) { }
        internal static void Warn(string tag, string msg) { }
        internal static void Error(string tag, string msg) { Note(tag + ": " + msg); }
        internal static void Error(string tag, string msg, Exception e) { Note(tag + ": " + msg + " " + e); }
        private static void Note(string s)
        {
            SimContext c = SimContext.Cur;
            if (c == null) return;
            c.Errors++; c.LastError = s;
            if (c.LogSink != null) c.LogSink((float)c.Time - c.LogT0, "ERROR", s);
        }
    }

    /// <summary>The part of BtcDuelMatch the bots read. Escaping/Ctrl are the sim's own arrays (the sim runs the rules).</summary>
    internal sealed class BtcDuelMatch
    {
        internal readonly List<BtcDuelUnitRef> Units = new List<BtcDuelUnitRef>();
        internal bool[] Escaping = new bool[2];
        internal float[] Ctrl = new float[2];
        internal readonly float[] AvgDmg = { 32f, 32f };
        internal float FightTime;
        internal int Winner = -1;
    }
}
