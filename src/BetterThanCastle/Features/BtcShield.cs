using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Engine;
using Brix.Game.Rules;
using Brix.Network;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Base shields (Gameplay Settings > Base shields, group "shield"). Spherical barriers around a player's home crystal and/or captured crystals.
    /// While a barrier is UP against a faction, that faction cannot enter (units are stopped at the edge), cannot damage anything inside (units, blocks,
    /// terrain: melee, arrows, digging, explosions, catapults), cannot capture the crystal, and its units caught inside when the barrier went up are
    /// powerless and take damage per second until they leave. The owner side fights out of its barrier freely.
    /// When a barrier is up (combinable): peace time (BtcTimer) = always up; then "drop by captured crystals" (the attacker, or its team, holds N
    /// captured crystals; the home crystal does not count) and/or "artificer suppression" (an artificer of the attacker sees the crystal; the barrier
    /// comes back a set delay after it left or died). No drop rule enabled = up only in peace (peace on) or always (peace off).
    /// Host-authoritative: the host decides everything and enforces it; clients only draw. BetterConnection ON only (rules come from BtcGameplay.E,
    /// vanilla values when BC is OFF; no handler, no send while OFF). Wire: mod msg 109 host->clients (state, 1 Hz and at once on change).
    /// </summary>
    internal static class BtcShield
    {
        internal const short MsgState = 109;

        internal sealed class Bar
        {
            internal int Key;                 // nest instance id (host) / position hash (client)
            internal FireflyNest Nest;        // host only
            internal Vector3 C;
            internal float R;
            internal int Owner;               // faction ID
            internal bool Home;
            internal bool UpAny;
            internal readonly List<int> Fac = new List<int>();      // enemy player factions of the owner (faction IDs)
            internal readonly List<bool> Up = new List<bool>();     // barrier up against Fac[i]
            internal readonly List<float> Secs = new List<float>(); // seconds to the next known change for Fac[i] (-1 = none)
            internal readonly List<byte> Why = new List<byte>();    // 0 none, 1 peace, 2 shards, 3 artificer, 4 permanent
            internal readonly Dictionary<int, float> MageSeen = new Dictionary<int, float>();
            internal readonly List<int> PFac = new List<int>();
            internal readonly List<bool> PUp = new List<bool>();
            internal int Hits;                // bumps / blocked shots (drives the flash + sound on every peer)
            internal bool Fresh = true;       // created this tick: everybody inside who is blocked becomes trapped
            internal bool Seen;

            internal bool UpFor(int fid)
            {
                for (int i = 0; i < Fac.Count; i++) if (Fac[i] == fid) return Up[i];
                return false;
            }

            internal int IndexOf(int fid) { for (int i = 0; i < Fac.Count; i++) if (Fac[i] == fid) return i; return -1; }
        }

        private sealed class Unit
        {
            internal Labor Lab;
            internal Locomotion4 Loco;
            internal BricktronDamageReceiver Dr;
            internal Vector3 LastOut;
            internal bool HasOut;
            internal int Trapped = int.MinValue;   // bar key the unit is trapped in
            internal float Dot;
            internal float NextHalt, NextKnock;
            internal int Knocks, Bounces;
            internal bool Seen;
        }

        internal static readonly List<Bar> Bars = new List<Bar>();
        private static readonly Dictionary<int, Bar> BarByKey = new Dictionary<int, Bar>();
        private static readonly Dictionary<int, Unit> Units = new Dictionary<int, Unit>();
        private static readonly List<Labor> LabTmp = new List<Labor>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly Dictionary<int, Faction> FacCache = new Dictionary<int, Faction>();
        private static readonly Dictionary<int, int> Shards = new Dictionary<int, int>();
        private static readonly List<Faction> Players = new List<Faction>();
        private static readonly List<Vector4> Mages = new List<Vector4>();   // xyz = position, w = range
        private static readonly List<int> MageFac = new List<int>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Host: at least one barrier is up against somebody (fast exit for the damage hooks).</summary>
        internal static bool Any;
        private static bool _wasOn;
        private static float _nextStep, _nextNest, _nextSend, _nextFacFlush, _nextLog;
        private static bool _dirty;
        private static int _hitsSent;

        private static int iOn = -1, iHome, iCapt, iRad, iDps, iPeace, iShards, iShardN, iTeam, iMage, iMageR, iMageD, iAi, iKnock = -1;

        // ------------------------------------------------------------------ rules
        internal static void AddRows()
        {
            const string G = "shield";
            BtcGameplay.Add("shield.on", G, "Base shields", "Защита базы", 0f, 0f, 1f, 1f, true,
                "Energy barriers around players' crystals. While a barrier is up, enemies cannot enter it, cannot damage, dig, blow up or capture anything inside; the owner fights out of it freely. Enemy units caught inside when it goes up are powerless and take damage until they leave. Critical rule change. Needs BetterConnection ON for everybody.",
                "Энергетические барьеры вокруг кристаллов игроков. Пока барьер активен, враг не может зайти внутрь, наносить урон, копать, взрывать и захватывать что-либо внутри; владелец спокойно бьёт изнутри наружу. Вражеские юниты, оказавшиеся внутри в момент включения, бессильны и получают урон, пока не выйдут. Критичное изменение правил. Нужен BetterConnection ВКЛ у всех.");
            BtcGameplay.Add("shield.home", G, "Shield the home crystal", "Барьер вокруг домашнего кристалла", 1f, 0f, 1f, 1f, true,
                "A barrier around every player's home crystal.", "Барьер вокруг домашнего кристалла каждого игрока.");
            BtcGameplay.Add("shield.capt", G, "Shield captured crystals", "Барьер вокруг захваченных кристаллов", 1f, 0f, 1f, 1f, true,
                "A barrier around every crystal a player has captured. It goes up as soon as the crystal is taken.", "Барьер вокруг каждого кристалла, захваченного игроком. Включается сразу после захвата.");
            BtcGameplay.Add("shield.radius", G, "Barrier radius (blocks)", "Радиус барьера (блоки)", 20f, 4f, 80f, 1f, false,
                "Sphere radius around the crystal. 20 = the range in which a crystal powers wards.", "Радиус сферы вокруг кристалла. 20 = дальность, в которой кристалл запитывает варды.");
            BtcGameplay.Add("shield.dps", G, "Damage to enemies caught inside (HP/s)", "Урон врагам внутри барьера (ХП/с)", 20f, 0f, 200f, 1f, false,
                "Enemy units that were inside when the barrier went up take this much damage per second until they walk out.", "Вражеские юниты, оказавшиеся внутри при включении барьера, получают столько урона в секунду, пока не выйдут.");
            BtcGameplay.Add("shield.knock", G, "Knockback on touch (0 = just stop)", "Отбрасывание при касании (0 = просто стоп)", 4f, 0f, 30f, 1f, false,
                "An enemy that walks or jumps into a barrier is knocked out of it by a weak smash (no damage), stronger the deeper it got. 0 = it is only stopped at the edge.", "Враг, который заходит или запрыгивает в барьер, вылетает из него от слабого смэша (без урона), тем сильнее, чем глубже зашёл. 0 = его просто останавливает у края.");
            BtcGameplay.Add("shield.peace", G, "Up during peace time", "Активны в мирное время", 1f, 0f, 1f, 1f, true,
                "Every barrier is up while the advanced timer shows peace (and while players get ready). Without a drop rule below, barriers fall when the peace ends.", "Все барьеры активны, пока продвинутый таймер показывает мирное время (и пока игроки жмут Готов). Без правил снятия ниже барьеры падают, когда мир кончается.");
            BtcGameplay.Add("shield.shards", G, "Drop by captured crystals", "Снятие по захваченным осколкам", 0f, 0f, 1f, 1f, true,
                "Enemy barriers fall for a player who holds the set number of captured crystals (the home crystal does not count). Lose them and the barriers are back.", "Вражеские барьеры падают для игрока, который держит заданное число захваченных кристаллов (домашний не считается). Потерял их - барьеры снова работают.");
            BtcGameplay.Add("shield.shardN", G, "Captured crystals needed", "Нужно захваченных кристаллов", 2f, 1f, 20f, 1f, false,
                "How many captured crystals open enemy barriers.", "Сколько захваченных кристаллов открывают вражеские барьеры.");
            BtcGameplay.Add("shield.team", G, "Count per team", "Считать по команде", 1f, 0f, 1f, 1f, true,
                "On: crystals and artificers of allies count for the whole team. Off: every player only by their own.", "Вкл: кристаллы и артефакторы союзников считаются на всю команду. Выкл: каждый игрок только по своим.");
            BtcGameplay.Add("shield.mage", G, "Artificers suppress barriers", "Артефактор подавляет барьеры", 0f, 0f, 1f, 1f, true,
                "Monarch mode: an enemy barrier falls for a player while one of their artificers sees it (its edge is within the artificer's reach).", "Монарх-мод: вражеский барьер падает для игрока, пока его артефактор видит барьер (край барьера в пределах дальности артефактора).");
            BtcGameplay.Add("shield.mageR", G, "Artificer reach (0 = its vision)", "Дальность артефактора (0 = его обзор)", 0f, 0f, 120f, 1f, false,
                "Distance from the artificer to the barrier's edge. 0 = the artificer's own vision range.", "Расстояние от артефактора до края барьера. 0 = собственный обзор артефактора.");
            BtcGameplay.Add("shield.mageD", G, "Back up after the artificer left (s)", "Включается после ухода артефактора (с)", 5f, 0f, 120f, 1f, false,
                "Seconds the barrier stays down after the last artificer left or died.", "Сколько секунд барьер остаётся снятым после ухода или смерти последнего артефактора.");
            BtcGameplay.Add("shield.ai", G, "Also stop corruptrons", "Останавливать и корраптронов", 0f, 0f, 1f, 1f, true,
                "Corruptrons and other AI enemies are stopped by barriers too.", "Корраптроны и другие ИИ-враги тоже останавливаются барьерами.");
            iOn = BtcGameplay.Idx("shield.on"); iHome = BtcGameplay.Idx("shield.home"); iCapt = BtcGameplay.Idx("shield.capt");
            iRad = BtcGameplay.Idx("shield.radius"); iDps = BtcGameplay.Idx("shield.dps"); iPeace = BtcGameplay.Idx("shield.peace");
            iShards = BtcGameplay.Idx("shield.shards"); iShardN = BtcGameplay.Idx("shield.shardN"); iTeam = BtcGameplay.Idx("shield.team");
            iMage = BtcGameplay.Idx("shield.mage"); iMageR = BtcGameplay.Idx("shield.mageR"); iMageD = BtcGameplay.Idx("shield.mageD"); iAi = BtcGameplay.Idx("shield.ai");
            iKnock = BtcGameplay.Idx("shield.knock");
        }

        private static float Val(int i, float def)
        {
            float[] e = BtcGameplay.E;
            return (e == null || i < 0 || i >= e.Length) ? def : e[i];
        }

        private static bool B(int i) { return Val(i, 0f) > 0.5f; }

        /// <summary>The rule is on in the current rules (host: own values, client: the host's).</summary>
        internal static bool RuleOn { get { return iOn >= 0 && BtcNetworkGate.Enabled && B(iOn); } }
        internal static float Radius { get { return Mathf.Max(1f, Val(iRad, 20f)); } }

        private static bool Host { get { return NetworkServer.active; } }

        internal static bool InPeace(out float left)
        {
            left = -1f;
            if (BtcTimer.Cur == BtcTimer.Phase.Waiting || BtcTimer.Cur == BtcTimer.Phase.Counting) return true;
            if (BtcTimer.Cur == BtcTimer.Phase.Running && BtcTimer.PeaceSec > 0f && BtcTimer.Elapsed < BtcTimer.PeaceSec) { left = BtcTimer.PeaceSec - BtcTimer.Elapsed; return true; }
            return false;
        }

        private static bool InMatch()
        {
            if (BtcGameplay.InLobby) return false;
            if (!GameSignals.Received<Action>(GameSignals.LevelReady)) return false;
            return BtcReplay.Mode != BtcReplay.State.Playing;
        }

        // ------------------------------------------------------------------ tick (ResourcePackManager.Update via BtcTimer.Tick)
        internal static void Tick()
        {
            try
            {
                bool on = RuleOn && InMatch();
                if (!on)
                {
                    if (_wasOn) Clear("off");
                    BtcShieldFx.Tick(false);
                    return;
                }
                if (!_wasOn) { _wasOn = true; BtcLog.Info("Shield", "on (host=" + Host + ")"); }
                long t0 = Sw.ElapsedTicks;
                if (Host)
                {
                    float now = Time.time;
                    if (now >= _nextStep)
                    {
                        float dt = _nextStep <= 0f ? 0.1f : Mathf.Min(0.5f, now - (_nextStep - 0.1f));
                        _nextStep = now + 0.1f;
                        long s0 = Sw.ElapsedTicks;
                        HostStep(now, dt);
                        long st = Sw.ElapsedTicks - s0;
                        if (st > _maxStep) _maxStep = st;
                    }
                    ProjTick();
                    float un = Time.unscaledTime;
                    if (_dirty && un >= _nextSend - 0.8f || un >= _nextSend) { _dirty = false; _nextSend = un + 1f; Broadcast(); }
                }
                long t1 = Sw.ElapsedTicks;
                BtcShieldFx.Tick(true);
                long t2 = Sw.ElapsedTicks;
                _tLogic += t1 - t0; _tFx += t2 - t1; _frames++;
                if (Time.unscaledTime >= _nextPerf) { _nextPerf = Time.unscaledTime + 10f; PerfLog(); }
            }
            catch (Exception e) { BtcLog.Error("Shield", "tick: " + e); }
        }

        // ------------------------------------------------------------------ cost (every 10 s in the log: average ms per frame, worst 10 Hz step, damage hooks)
        private static readonly System.Diagnostics.Stopwatch Sw = System.Diagnostics.Stopwatch.StartNew();
        private static long _tLogic, _tFx, _tVeto, _maxStep;
        private static int _frames, _nVeto;
        private static float _nextPerf;

        private static void PerfLog()
        {
            if (_frames == 0) return;
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            BtcLog.Info("Shield", "cost frames=" + _frames + " logic=" + (_tLogic * ms / _frames).ToString("F3", Inv) + "ms/f fx=" + (_tFx * ms / _frames).ToString("F3", Inv)
                + "ms/f worstStep=" + (_maxStep * ms).ToString("F2", Inv) + "ms hooks=" + _nVeto + " hooksTotal=" + (_tVeto * ms).ToString("F2", Inv) + "ms bars=" + Bars.Count + " units=" + Units.Count);
            _tLogic = _tFx = _tVeto = _maxStep = 0; _frames = 0; _nVeto = 0;
        }

        private static void Clear(string why)
        {
            _wasOn = false;
            Any = false;
            Bars.Clear();
            BarByKey.Clear();
            Units.Clear();
            FacCache.Clear();
            Projs.Clear();
            _nextStep = 0f; _nextNest = 0f;
            BtcLog.Info("Shield", "cleared (" + why + ")");
        }

        // ------------------------------------------------------------------ host
        private static void HostStep(float now, float dt)
        {
            if (now >= _nextFacFlush) { _nextFacFlush = now + 3f; FacCache.Clear(); UnitCache.Clear(); }
            if (now >= _nextNest) { _nextNest = now + 1f; RefreshNests(); }
            float r = Radius;
            for (int i = 0; i < Bars.Count; i++) Bars[i].R = r;
            CollectUnits();
            ComputeStates(now);
            StepUnits(dt, now);
            for (int i = 0; i < Bars.Count; i++) Bars[i].Fresh = false;
            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 10f;
                int up = 0, trapped = 0;
                for (int i = 0; i < Bars.Count; i++) if (Bars[i].UpAny) up++;
                foreach (KeyValuePair<int, Unit> kv in Units) if (kv.Value.Trapped != int.MinValue) trapped++;
                BtcLog.Info("Shield", "bars=" + Bars.Count + " up=" + up + " units=" + Units.Count + " trapped=" + trapped + " proj=" + Projs.Count);
            }
        }

        private static bool IsPlayer(Faction f) { return BtcMatchStats.Tracks(f) && !f.isAI; }

        private static bool Allied(Faction a, Faction b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return a.AllyFactions != null && a.AllyFactions.Contains(b);
        }

        private static void RefreshNests()
        {
            bool home = B(iHome), capt = B(iCapt);
            float r = Radius;
            List<FireflyNest> list = BtcMatchStats.NestList();
            for (int i = 0; i < Bars.Count; i++) Bars[i].Seen = false;
            Shards.Clear();
            Players.Clear();
            List<Faction> all = Faction.GetAllFactions();
            if (all != null) for (int i = 0; i < all.Count; i++) if (IsPlayer(all[i]) && (all[i].HasOwner() || all[i].UnitCount > 0)) Players.Add(all[i]);   // empty lobby slots are not enemies
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    FireflyNest n = list[i];
                    if (n == null) continue;
                    Faction f = n.faction;
                    if (!IsPlayer(f)) continue;
                    bool isHome = n.isHome;
                    if (!isHome) { int c; Shards.TryGetValue(f.ID, out c); Shards[f.ID] = c + 1; }
                    if (isHome ? !home : !capt) continue;
                    int key = n.gameObject.GetInstanceID();
                    Bar b;
                    if (!BarByKey.TryGetValue(key, out b) || b.Owner != f.ID)
                    {
                        if (b != null) Bars.Remove(b);
                        b = new Bar { Key = key, Nest = n, Owner = f.ID, Home = isHome };
                        BarByKey[key] = b;
                        Bars.Add(b);
                        _dirty = true;
                        BtcLog.Info("Shield", "bar + " + n.name + " owner=" + f.ID + " home=" + isHome);
                    }
                    b.Seen = true;
                    b.C = n.transform.position;
                    b.R = r;
                    b.Home = isHome;
                }
            }
#if DEV
            for (int i = 0; i < DevBars.Count; i++)
            {
                Bar d = DevBars[i];
                if (!BarByKey.ContainsKey(d.Key)) { BarByKey[d.Key] = d; Bars.Add(d); _dirty = true; }
                d.Seen = true;
                d.R = r;
            }
#endif
            for (int i = Bars.Count - 1; i >= 0; i--)
            {
                if (Bars[i].Seen) continue;
                BtcLog.Info("Shield", "bar - owner=" + Bars[i].Owner);
                BarByKey.Remove(Bars[i].Key);
                Bars.RemoveAt(i);
                _dirty = true;
            }
        }

#if DEV
        /// <summary>DEV lab: barriers at arbitrary points (duel arena has no owned crystals).</summary>
        internal static readonly List<Bar> DevBars = new List<Bar>();
        internal static void DevAddBar(int fid, Vector3 c) { DevBars.Add(new Bar { Key = -1000 - DevBars.Count, Owner = fid, C = c, R = Radius }); _nextNest = 0f; }

        internal static string DevDump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("any=").Append(Any).Append(" bars=").Append(Bars.Count).Append(" proj=").Append(Projs.Count);
            for (int i = 0; i < Bars.Count; i++)
            {
                Bar b = Bars[i];
                sb.Append(" | owner=").Append(b.Owner).Append(" r=").Append(b.R.ToString("F0", Inv)).Append(" hits=").Append(b.Hits).Append(" up=");
                for (int k = 0; k < b.Fac.Count; k++) sb.Append(b.Fac[k]).Append(':').Append(b.Up[k] ? 1 : 0).Append('/').Append(b.Why[k]).Append('/').Append(b.Secs[k].ToString("F0", Inv)).Append(' ');
            }
            foreach (KeyValuePair<int, Unit> kv in Units)
            {
                Unit u = kv.Value;
                if (u.Lab == null) continue;
                Faction f = u.Lab.faction;
                Vector3 p = u.Lab.transform.position;
                Bar last = Bars.Count > 0 ? Bars[Bars.Count - 1] : null;
                float dist = last != null ? (p - last.C).magnitude : -1f;
                sb.Append(" || u").Append(kv.Key).Append(" f=").Append(f != null ? f.ID : -1).Append(" hp=").Append(u.Dr != null ? u.Dr.CurrentHP.ToString("F0", Inv) : "?")
                  .Append(" d=").Append(dist.ToString("F1", Inv)).Append(" dy=").Append(last != null ? (p.y - last.C.y).ToString("F1", Inv) : "?")
                  .Append(u.Loco != null && u.Loco.IsAirborne ? " AIR" : "").Append(" kn=").Append(u.Knocks).Append(" b=").Append(u.Bounces).Append(u.Trapped != int.MinValue ? " TRAP" : "");
            }
            return sb.ToString();
        }
#endif

        private static int ShardsOf(Faction f, bool team)
        {
            int n;
            Shards.TryGetValue(f.ID, out n);
            if (!team) return n;
            for (int i = 0; i < Players.Count; i++)
            {
                Faction o = Players[i];
                if (o == f || !Allied(f, o)) continue;
                int k; if (Shards.TryGetValue(o.ID, out k)) n += k;
            }
            return n;
        }

        private static void CollectUnits()
        {
            LabTmp.Clear();
            BtcRegistry.CollectLabors(LabTmp);
            foreach (KeyValuePair<int, Unit> kv in Units) kv.Value.Seen = false;
            Mages.Clear();
            MageFac.Clear();
            bool mage = B(iMage);
            float reach = Val(iMageR, 0f);
            for (int i = 0; i < LabTmp.Count; i++)
            {
                Labor l = LabTmp[i];
                if (l == null) continue;
                int id = l.gameObject.GetInstanceID();
                Unit u;
                if (!Units.TryGetValue(id, out u))
                {
                    u = new Unit { Lab = l, Loco = l.GetComponent<Locomotion4>(), Dr = l.GetComponent<BricktronDamageReceiver>() };
                    Units[id] = u;
                }
                u.Seen = true;
                if (!mage || u.Dr == null || u.Dr.CurrentHP <= 0f) continue;
                try
                {
                    if (l.Occupation == null || l.Occupation.CurrentJob != Occupation.Job.Artificer) continue;
                    Faction f = l.faction;
                    if (!IsPlayer(f)) continue;
                    float range = reach;
                    if (range <= 0f)
                    {
                        LocalFactionVision v = l.vision;
                        range = v == null ? 20f : (v.UseGameRuleLineOfSight && GeneralRules.Main != null ? GeneralRules.Main.FogOfWarDistance : v.LineOfSightRange);
                    }
                    Vector3 p = l.transform.position;
                    Mages.Add(new Vector4(p.x, p.y, p.z, range));
                    MageFac.Add(f.ID);
                }
                catch { }
            }
            Gone.Clear();
            foreach (KeyValuePair<int, Unit> kv in Units) if (!kv.Value.Seen || kv.Value.Lab == null) Gone.Add(kv.Key);
            for (int i = 0; i < Gone.Count; i++) Units.Remove(Gone[i]);
        }

        private static void ComputeStates(float now)
        {
            bool peaceMode = B(iPeace), shards = B(iShards), mage = B(iMage), team = B(iTeam);
            int need = Mathf.Max(1, Mathf.RoundToInt(Val(iShardN, 2f)));
            float delay = Val(iMageD, 5f);
            float peaceLeft = -1f;
            bool peace = peaceMode && InPeace(out peaceLeft);
            if (!peace) peaceLeft = -1f;
            bool drop = shards || mage;
            bool any = false;
            for (int b = 0; b < Bars.Count; b++)
            {
                Bar bar = Bars[b];
                Faction owner = BtcMatchStats.FactionOf(bar.Owner);
                // enemies of the owner (previous state kept to detect flips)
                bar.PFac.Clear(); bar.PFac.AddRange(bar.Fac);
                bar.PUp.Clear(); bar.PUp.AddRange(bar.Up);
                bar.Fac.Clear(); bar.Up.Clear(); bar.Secs.Clear(); bar.Why.Clear();
                bar.UpAny = false;
                for (int p = 0; p < Players.Count; p++)
                {
                    Faction f = Players[p];
                    if (owner == null || Allied(owner, f) || Allied(f, owner)) continue;
                    bool up; float secs = -1f; byte why;
                    if (peace) { up = true; secs = peaceLeft; why = 1; }
                    else if (!drop) { up = !peaceMode; why = (byte)(up ? 4 : 0); }
                    else
                    {
                        bool down = false; why = 0;
                        if (shards && ShardsOf(f, team) >= need) { down = true; why = 2; }
                        if (mage)
                        {
                            bool seenNow = false;
                            for (int m = 0; m < Mages.Count; m++)
                            {
                                int mf = MageFac[m];
                                if (mf != f.ID && !(team && Allied(f, BtcMatchStats.FactionOf(mf)))) continue;
                                Vector4 mp = Mages[m];
                                float reach = mp.w + bar.R;   // the artificer sees the barrier's edge
                                if ((new Vector3(mp.x, mp.y, mp.z) - bar.C).sqrMagnitude <= reach * reach) { seenNow = true; break; }
                            }
                            if (seenNow) bar.MageSeen[f.ID] = now;
                            float seen;
                            if (bar.MageSeen.TryGetValue(f.ID, out seen) && now - seen <= delay)
                            {
                                if (!down) { why = 3; if (!seenNow) secs = delay - (now - seen); }
                                down = true;
                            }
                        }
                        up = !down;
                    }
                    bar.Fac.Add(f.ID); bar.Up.Add(up); bar.Secs.Add(secs); bar.Why.Add(why);
                    if (up) bar.UpAny = true;
                }
                if (!bar.Fresh)
                {
                    for (int i = 0; i < bar.Fac.Count; i++)
                    {
                        int k = bar.PFac.IndexOf(bar.Fac[i]);
                        bool was = k >= 0 && k < bar.PUp.Count && bar.PUp[k];
                        if (was != bar.Up[i]) { _dirty = true; OnFlip(bar, bar.Fac[i], bar.Up[i]); }
                    }
                }
                if (bar.UpAny) any = true;
            }
            Any = any;
        }

        /// <summary>Faction fid: the barrier just went up (trap everybody of fid inside) or down.</summary>
        private static void OnFlip(Bar bar, int fid, bool up)
        {
            BtcLog.Info("Shield", "flip owner=" + bar.Owner + " vs " + fid + " -> " + (up ? "UP" : "down"));
            if (!up) return;
            foreach (KeyValuePair<int, Unit> kv in Units)
            {
                Unit u = kv.Value;
                if (u.Lab == null) continue;
                Faction f = u.Lab.faction;
                if (f == null || f.ID != fid) continue;
                if (In(bar, u.Lab.transform.position)) u.Trapped = bar.Key;
            }
        }

        internal static bool In(Bar b, Vector3 p) { return (p - b.C).sqrMagnitude <= b.R * b.R; }

        private static bool InFriendly(Faction f, Vector3 p)
        {
            for (int i = 0; i < Bars.Count; i++)
            {
                Bar b = Bars[i];
                if (b.Owner != f.ID && !Allied(f, BtcMatchStats.FactionOf(b.Owner))) continue;
                if (In(b, p)) return true;
            }
            return false;
        }

        /// <summary>Barrier b is up against faction f (player: per faction state; AI: when the rule is on and the barrier is up against anybody).</summary>
        private static bool Blocks(Bar b, Faction f)
        {
            if (f == null) return false;
            if (f.ID == b.Owner) return false;
            if (IsPlayer(f)) return b.UpFor(f.ID);
            if (f.IsSpectator) return false;
            return b.UpAny && B(iAi) && f.isAI;
        }

        private static void StepUnits(float dt, float now)
        {
            float dps = Val(iDps, 20f);
            foreach (KeyValuePair<int, Unit> kv in Units)
            {
                Unit u = kv.Value;
                Labor l = u.Lab;
                if (l == null || u.Dr == null || u.Dr.CurrentHP <= 0f) continue;
                Faction f = l.faction;
                if (f == null) continue;
                Vector3 p = l.transform.position;
                Bar hit = null;
                bool trappedStill = false;
                for (int i = 0; i < Bars.Count; i++)
                {
                    Bar b = Bars[i];
                    if (!In(b, p) || !Blocks(b, f)) continue;
                    if (u.Trapped == b.Key || b.Fresh) { u.Trapped = b.Key; trappedStill = true; break; }
                    hit = b;
                }
                if (trappedStill)
                {
                    if (dps > 0f)
                    {
                        u.Dot += dps * dt;
                        if (u.Dot >= 1f)
                        {
                            float d = Mathf.Floor(u.Dot);
                            u.Dot -= d;
                            try { u.Dr.ApplyDamage(d, null); } catch (Exception e) { BtcLog.Warn("Shield", "dot: " + e.Message); }
                        }
                    }
                    continue;
                }
                if (u.Trapped != int.MinValue) { u.Trapped = int.MinValue; u.Dot = 0f; }
                bool air = u.Loco != null && u.Loco.IsAirborne;
                if (hit == null)
                {
                    // last safe outside point: on the ground, and not while still flying from a knock
                    bool grounded = u.Loco == null || u.Loco.IsInGroundedState();
                    if (grounded && now >= u.NextKnock) { u.LastOut = p; u.HasOut = true; u.Knocks = 0; u.Bounces = 0; }
                    continue;
                }
                if (InFriendly(f, p)) continue;   // overlapping barriers: a unit inside its own team's barrier is not pushed
                if (u.Loco != null && u.Loco.IsGrabbed) continue;   // carried: wait until it is dropped
                if (!air && u.Knocks == 0 && (!u.HasOut || In(hit, u.LastOut))) { u.Trapped = hit.Key; continue; }   // was never outside: caught inside
                Repel(u, hit, now, p, air, kv.Key);
            }
        }

        /// <summary>A blocked unit touching a barrier. On the ground: a weak smash out of the sphere (no damage, a bit stronger the deeper it got).
        /// In the air (jumping or falling onto the dome; the game ignores smashes in the air): moved to the dome surface at the same height,
        /// so it keeps falling outside and lands where there is no barrier. Knockback 0, or still inside after the smash had its time:
        /// put back on its last outside ground point (or the surface point when there is none).</summary>
        private static void Repel(Unit u, Bar bar, float now, Vector3 p, bool air, int id)
        {
            try
            {
                if (now < u.NextKnock) return;   // a smash is still carrying it out
                float knock = Val(iKnock, 4f);
                Vector3 n = p - bar.C;
                Vector3 h = new Vector3(n.x, 0f, n.z);
                if (h.sqrMagnitude < 0.25f) h = new Vector3(Mathf.Sin(id * 0.7f), 0f, Mathf.Cos(id * 0.7f));   // straight above the crystal: any side
                h.Normalize();
                Rigidbody body = u.Loco != null && u.Loco.IsFlailing() ? u.Loco.FlailingColliderRigidbody : null;
                if (body != null && !body.isKinematic && u.Bounces < 8)
                {
                    // flying ragdoll (smashed, thrown or falling after a smash) inside the dome: kick it off the surface, out and up;
                    // it lands on the dome again and bounces until it comes down where there is no barrier
                    u.Bounces++;
                    Vector3 nn = n.sqrMagnitude > 0.01f ? n.normalized : Vector3.up;
                    body.position = bar.C + nn * (bar.R + 0.4f);
                    float k = Mathf.Max(2f, knock);
                    body.velocity = h * (4f + 0.9f * k) + Vector3.up * (3f + 0.6f * k);
                    u.NextKnock = now + 0.25f;
                }
                else if (air && n.y > 1.5f && u.Bounces < 8)
                {
                    // jumping or falling onto the dome: a smash turns it into a flying ragdoll (no damage), the next step kicks it off
                    u.Bounces++;
                    u.Dr.ReceiveAttack(0f, DamageType.Explosive, DamageReaction.Smash, p - h, (h + Vector3.up * 0.5f).normalized * Mathf.Max(2f, knock), 0.5f, null);
                    u.NextKnock = now + 0.05f;
                }
                else if (knock > 0f && u.Knocks < 2 && !air)
                {
                    u.Knocks++;
                    u.NextKnock = now + 1.2f;
                    float depth = Mathf.Clamp(bar.R - n.magnitude, 0f, 10f);
                    Vector3 dir = (h + Vector3.up * 0.35f).normalized;
                    float force = knock * (1f + 0.15f * depth);
                    // Explosive type: always a smash (no shield block), 0 damage; attacker null = not vetoed by our own hooks
                    u.Dr.ReceiveAttack(0f, DamageType.Explosive, DamageReaction.Smash, p - h, dir * force, 0.5f, null);
                }
                else
                {
                    // the smash did not get it out (or knockback is off, or it lies knocked down inside): put it outside
                    Vector3 to = u.HasOut && !In(bar, u.LastOut) ? u.LastOut : bar.C + h * (Mathf.Sqrt(Mathf.Max(0f, bar.R * bar.R - n.y * n.y)) + 1f) + Vector3.up * n.y;
                    if (u.Loco != null) u.Loco.SetPosition(to, true);
                    u.Knocks = 0;
                    u.NextKnock = now + 0.3f;
                }
                if (now >= u.NextHalt)
                {
                    u.NextHalt = now + 0.6f;
                    Order.Move.HaltWhatever.Send(u.Lab, Labor.DecisionSource.User);
                }
                bar.Hits++;
                _dirty = true;
            }
            catch (Exception e) { BtcLog.Warn("Shield", "repel: " + e.Message); }
        }

        // ------------------------------------------------------------------ hooks (host)
        private static readonly Dictionary<int, bool> UnitCache = new Dictionary<int, bool>();

        internal static Faction FacOf(GameObject go)
        {
            if (go == null) return null;
            int id = go.GetInstanceID();
            Faction f;
            if (FacCache.TryGetValue(id, out f)) return f;
            try { f = Faction.GetFaction(go); } catch { f = null; }
            FacCache[id] = f;
            UnitCache[id] = go.GetComponent<Labor>() != null;
            return f;
        }

        /// <summary>The attacker's side is known: a player faction, or an AI unit (an AI-coloured object such as an explosion is not trusted).</summary>
        private static bool Known(GameObject go, Faction f)
        {
            if (f == null) return false;
            if (IsPlayer(f)) return true;
            bool unit;
            return f.isAI && UnitCache.TryGetValue(go.GetInstanceID(), out unit) && unit;
        }

        /// <summary>Host: true = this damage must not happen. target = world position of what is hit, attacker may be null (traps, the barrier itself).</summary>
        internal static bool Veto(GameObject attacker, Vector3 target)
        {
            if (!Any || attacker == null || !Host) return false;
            long v0 = Sw.ElapsedTicks;
            bool r = VetoCore(attacker, target);
            _tVeto += Sw.ElapsedTicks - v0; _nVeto++;
            return r;
        }

        private static bool VetoCore(GameObject attacker, Vector3 target)
        {
            Faction f = FacOf(attacker);
            Vector3 ap = attacker.transform.position;
            bool known = Known(attacker, f);
            for (int i = 0; i < Bars.Count; i++)
            {
                Bar b = Bars[i];
                if (known)
                {
                    if (!Blocks(b, f)) continue;
                    if (In(b, target) || In(b, ap)) { Bump(b); return true; }
                }
                else if (b.UpAny && In(b, target) && !In(b, ap)) { Bump(b); return true; }   // no faction (explosion object...): nothing from outside gets in
            }
            return false;
        }

        private static float _lastBump;
        private static void Bump(Bar b)
        {
            if (Time.unscaledTime - _lastBump < 0.15f) return;
            _lastBump = Time.unscaledTime;
            b.Hits++;
            _dirty = true;
        }

        /// <summary>Host: drop the units of factions blocked by a barrier around this capture zone (no capture, no decapture inside).</summary>
        internal static void FilterCapture(Component clock, HashSet<GameObject> units)
        {
            if (!Any || units == null || units.Count == 0 || clock == null) return;
            Vector3 c = clock.transform.position;
            for (int i = 0; i < Bars.Count; i++)
            {
                Bar b = Bars[i];
                if (!b.UpAny || !In(b, c)) continue;
                units.RemoveWhere(delegate (GameObject go) { return go == null || Blocks(b, FacOf(go)); });
            }
        }

        // ------------------------------------------------------------------ projectiles (host)
        private sealed class Proj { internal Rigidbody Rb; internal GameObject Go; internal Faction F; internal Vector3 From; internal int Bounces; }
        private static readonly List<Proj> Projs = new List<Proj>();

        internal static void AddCatapultShot(Component shot, Component catapult)
        {
            if (!Host || !_wasOn || shot == null) return;
            Rigidbody rb = shot.GetComponent<Rigidbody>();
            if (rb == null) return;
            Faction f = null;
            try { if (catapult != null) f = FacOf(catapult.gameObject); } catch { }
            if (Projs.Count > 64) Projs.RemoveAt(0);
            Projs.Add(new Proj { Rb = rb, Go = shot.gameObject, F = f, From = shot.transform.position });
        }

        private static void ProjTick()
        {
            if (Projs.Count == 0) return;
            for (int i = Projs.Count - 1; i >= 0; i--)
            {
                Proj pr = Projs[i];
                if (pr.Go == null || pr.Rb == null || !pr.Go.activeInHierarchy) { Projs.RemoveAt(i); continue; }
                if (!Any) continue;
                Vector3 p = pr.Go.transform.position;
                for (int k = 0; k < Bars.Count; k++)
                {
                    Bar b = Bars[k];
                    if (!In(b, p)) continue;
                    bool block = IsPlayer(pr.F) ? Blocks(b, pr.F) : (b.UpAny && !In(b, pr.From));
                    if (!block) continue;
                    Bump(b);
                    if (pr.Bounces++ < 2)
                    {
                        // bounce off the sphere: reflect the velocity, keep a little energy, put it back on the surface
                        Vector3 n = (p - b.C).normalized;
                        Vector3 v = pr.Rb.velocity;
                        float d = Vector3.Dot(v, n);
                        if (d < 0f) v = (v - 2f * d * n) * 0.45f;
                        pr.Rb.velocity = v;
                        pr.Go.transform.position = b.C + n * (b.R + 0.6f);
                    }
                    else
                    {
                        NetworkServer.Destroy(pr.Go);
                        Projs.RemoveAt(i);
                    }
                    break;
                }
            }
        }

        /// <summary>Host: an arrow/bolt (kinematic projectile) entering a barrier that blocks its shooter is removed at the edge.</summary>
        internal static bool StopArrow(Component proj, GameObject attacker)
        {
            if (!Any || !Host || proj == null) return false;
            Vector3 p = proj.transform.position;
            Faction f = FacOf(attacker);
            Vector3 from = attacker != null ? attacker.transform.position : p;
            bool known = attacker != null && Known(attacker, f);
            for (int i = 0; i < Bars.Count; i++)
            {
                Bar b = Bars[i];
                if (!In(b, p)) continue;
                bool block = known ? Blocks(b, f) && !In(b, from) : (b.UpAny && !In(b, from));
                if (!block) continue;
                Bump(b);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnState);
                }
            }
            catch (Exception e) { BtcLog.Error("Shield", "RegisterHandlers: " + e); }
        }

        private static string Encode()
        {
            StringBuilder sb = new StringBuilder(64 + Bars.Count * 48);
            sb.Append('1');
            for (int i = 0; i < Bars.Count; i++)
            {
                Bar b = Bars[i];
                sb.Append('|').Append(b.C.x.ToString("F1", Inv)).Append(',').Append(b.C.y.ToString("F1", Inv)).Append(',').Append(b.C.z.ToString("F1", Inv))
                  .Append(';').Append(b.R.ToString("F1", Inv)).Append(';').Append(b.Owner).Append(';').Append(b.Home ? 1 : 0).Append(';').Append(b.Hits).Append(';');
                for (int k = 0; k < b.Fac.Count; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(b.Fac[k]).Append(':').Append(b.Up[k] ? 1 : 0).Append(':').Append(b.Why[k]).Append(':').Append(Mathf.Max(-1f, b.Secs[k]).ToString("F0", Inv));
                }
            }
            return sb.ToString();
        }

        private static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringMessage msg = new StringMessage(Encode());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("Shield", "Broadcast: " + e); }
        }

        private static float _gotAt;

        private static void OnState(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (NetworkServer.active || !BtcNetworkGate.Enabled || BtcReplay.Mode == BtcReplay.State.Playing) return;
                string[] parts = msg.value.Split('|');
                if (parts.Length == 0 || parts[0] != "1") return;
                for (int i = 0; i < Bars.Count; i++) Bars[i].Seen = false;
                for (int i = 1; i < parts.Length; i++)
                {
                    string[] a = parts[i].Split(';');
                    if (a.Length < 6) continue;
                    string[] xyz = a[0].Split(',');
                    Vector3 c = new Vector3(float.Parse(xyz[0], Inv), float.Parse(xyz[1], Inv), float.Parse(xyz[2], Inv));
                    int key = Mathf.RoundToInt(c.x) * 73856093 ^ Mathf.RoundToInt(c.y) * 19349663 ^ Mathf.RoundToInt(c.z) * 83492791;
                    Bar b;
                    if (!BarByKey.TryGetValue(key, out b)) { b = new Bar { Key = key }; BarByKey[key] = b; Bars.Add(b); }
                    b.Seen = true;
                    b.C = c;
                    b.R = float.Parse(a[1], Inv);
                    b.Owner = int.Parse(a[2], Inv);
                    b.Home = a[3] == "1";
                    b.Hits = int.Parse(a[4], Inv);
                    b.Fac.Clear(); b.Up.Clear(); b.Why.Clear(); b.Secs.Clear();
                    b.UpAny = false;
                    if (a[5].Length > 0)
                    {
                        string[] fs = a[5].Split(',');
                        for (int k = 0; k < fs.Length; k++)
                        {
                            string[] q = fs[k].Split(':');
                            if (q.Length < 4) continue;
                            b.Fac.Add(int.Parse(q[0], Inv)); bool up = q[1] == "1"; b.Up.Add(up); b.Why.Add(byte.Parse(q[2], Inv)); b.Secs.Add(float.Parse(q[3], Inv));
                            if (up) b.UpAny = true;
                        }
                    }
                }
                for (int i = Bars.Count - 1; i >= 0; i--) if (!Bars[i].Seen) { BarByKey.Remove(Bars[i].Key); Bars.RemoveAt(i); }
                _gotAt = Time.unscaledTime;
                if (!_wasOn) { _wasOn = true; }
            }
            catch (Exception e) { BtcLog.Error("Shield", "OnState: " + e.Message); }
        }

        /// <summary>Seconds left in a countdown sent by the host, corrected for the time since the last state (clients) or exact (host).</summary>
        internal static float Left(float secs)
        {
            if (secs < 0f) return -1f;
            if (Host) return secs;
            return Mathf.Max(0f, secs - (Time.unscaledTime - _gotAt));
        }

        internal static int LocalFaction()
        {
            User u = User.LocalUser;
            return u != null && u.faction != null ? u.faction.ID : int.MinValue;
        }

        /// <summary>True when faction fid and the barrier owner are on the same side.</summary>
        internal static bool Friendly(Bar b, int fid)
        {
            if (fid == int.MinValue) return false;
            if (fid == b.Owner) return true;
            Faction a = BtcMatchStats.FactionOf(fid), o = BtcMatchStats.FactionOf(b.Owner);
            return Allied(a, o) || Allied(o, a);
        }
    }
}
