using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Game.Rules;
using Brix.Game.Semantique;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    /// <summary>One time-series point of a faction (all raw values at that moment).</summary>
    internal sealed class BtcMsSample
    {
        internal int Idx;
        internal float T;
        internal int[] V;
    }

    /// <summary>Per-faction stats: current raw values + the whole match history (sampled every 5 s).</summary>
    internal sealed class BtcMsFaction
    {
        internal readonly int Id;
        internal readonly int[] V = new int[BtcMatchStats.NV];
        internal readonly List<BtcMsSample> Series = new List<BtcMsSample>();
        /// <summary>Per-item breakdown (BtcMatchStats.D_*): item/structure key -> count; DetVer bumps on every change of that category.</summary>
        internal readonly Dictionary<string, int>[] Det = new Dictionary<string, int>[BtcMatchStats.ND];
        internal readonly int[] DetVer = new int[BtcMatchStats.ND];

        internal BtcMsFaction(int id)
        {
            Id = id;
            for (int i = 0; i < Det.Length; i++) Det[i] = new Dictionary<string, int>();
        }

        internal void AddDet(int cat, string key, int n)
        {
            if (string.IsNullOrEmpty(key) || n <= 0) return;
            int had;
            Det[cat].TryGetValue(key, out had);
            Det[cat][key] = had + n;
            DetVer[cat]++;
        }

        /// <summary>Insert by sample index (network delivery may be out of order); same index replaces.</summary>
        internal void AddSample(int idx, float t, int[] v)
        {
            BtcMsSample s = new BtcMsSample { Idx = idx, T = t, V = v };
            int n = Series.Count;
            if (n == 0 || Series[n - 1].Idx < idx) { Series.Add(s); return; }
            int i = Find(idx);
            if (i >= 0) { Series[i] = s; return; }
            int at = ~i;
            Series.Insert(at, s);
        }

        /// <summary>Binary search by Idx: index or ~insertion point.</summary>
        internal int Find(int idx)
        {
            int lo = 0, hi = Series.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int m = Series[mid].Idx;
                if (m == idx) return mid;
                if (m < idx) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }

        /// <summary>Latest sample with T &lt;= t (null when none).</summary>
        internal BtcMsSample At(float t)
        {
            for (int i = Series.Count - 1; i >= 0; i--) if (Series[i].T <= t) return Series[i];
            return null;
        }
    }

    /// <summary>A set of faction stats for one match (local collector or data received from the host).</summary>
    internal sealed class BtcMsStore
    {
        internal int MatchId;
        internal float T;
        internal int Version;
        internal readonly List<BtcMsFaction> Factions = new List<BtcMsFaction>();

        internal BtcMsFaction Get(int id, bool create)
        {
            for (int i = 0; i < Factions.Count; i++) if (Factions[i].Id == id) return Factions[i];
            if (!create) return null;
            BtcMsFaction f = new BtcMsFaction(id);
            Factions.Add(f);
            return f;
        }

        internal void Clear(int matchId)
        {
            MatchId = matchId;
            T = 0f;
            Factions.Clear();
            Version++;
        }
    }

    /// <summary>
    /// Match statistics collector (docs/decisions.md "Match statistics").
    /// Authoritative mode (NetworkServer.active, not replay playback): Harmony events (Patches/MatchStatsPatches.cs: unit kill, damage
    /// involvement, structure built, recipe finished) + 1 Hz polls. Light mode (pure client, replay playback): only replicated state
    /// is polled (units/HP/profession, crystals, stockpile content); trades by the proximity rule; idle/crafted/built/lost are n/a.
    /// Nothing here touches the network: the wire part is Net/BtcMatchStatsNet.cs (BetterConnection ON only).
    /// </summary>
    internal static class BtcMatchStats
    {
        // ---- raw value layout (also the wire layout; append only) ----
        internal const int V_LOSS = 0, V_OWNED = 1, V_ALIVE = 2, V_ENERGY = 3, V_CRYS = 4, V_CRYSEC = 5, V_HOLDSUM = 6, V_HOLDN = 7,
            V_IDLE = 8, V_TRADE = 9, V_FREE = 10, V_CRAFT = 11, V_BUILT = 12, V_ILOST = 13, V_BLOST = 14, V_SCORE = 15, V_AUTH = 16,
            V_PROF0 = 17, NPROF = 7, V_RES0 = V_PROF0 + NPROF, NRES = 12, V_BARREL = V_RES0 + NRES, NV = V_BARREL + 1;

        // per-item breakdown categories (BtcMsFaction.Det)
        internal const int D_CRAFT = 0, D_BUILT = 1, D_ILOST = 2, D_BLOST = 3, ND = 4;

        internal const float SampleEvery = 5f;
        internal const float TradeWindow = 5f;
        internal const float TradeRadius = 15f;
        internal const float InvolveWindow = 10f;

        /// <summary>Profession slots (display order): worker, halberdier, archer, arbalist, knight, alchemist, artificer.</summary>
        internal static int JobSlot(Occupation.Job j)
        {
            switch (j)
            {
                case Occupation.Job.Builder: return 0;
                case Occupation.Job.Halberdier: return 1;
                case Occupation.Job.Archer: return 2;
                case Occupation.Job.Arbalist: return 3;
                case Occupation.Job.Knight: return 4;
                case Occupation.Job.Alchemist: return 5;
                case Occupation.Job.Artificer: return 6;
            }
            return 0;
        }

        // resource slots: vanilla Ressource type names (Description.DicoAdjectif keys); RawMs = bricktron-time per unit (docs/stats/brick-time.csv), 0 = refined (scored via crafting)
        internal static readonly string[] ResTypes = { "Stones", "WoodBlock", "RawIron", "OrangeCrystal", "Plant", "PlankBlock", "StoneBlock", "Iron", "Glass", "Cog", "Rope", "Fabric" };
        internal static readonly int[] RawMs = { 102, 1878, 718, 718, 1316, 0, 0, 0, 0, 0, 0, 0 };

        internal static readonly BtcMsStore Local = new BtcMsStore();
        internal static readonly BtcMsStore Remote = new BtcMsStore();
        internal static float LastRemote;

        /// <summary>Host data is used while it keeps arriving (BC ON host); otherwise the local collector.</summary>
        internal static bool RemoteFresh
        {
            get
            {
                // replay playback: stats recorded in the file stay valid while paused or seeking
                if (BtcReplay.Mode == BtcReplay.State.Playing) return Remote.MatchId != 0;
                return Remote.MatchId != 0 && LastRemote > 0f && Time.unscaledTime - LastRemote < 6f && !NetworkServer.active;
            }
        }

        internal static BtcMsStore Current { get { return RemoteFresh ? Remote : Local; } }

        internal static bool Active { get { return _active; } }
        internal static bool Authoritative { get { return _auth; } }
        internal static int SampleCount { get { return _sampleIdx; } }

        // ---- state ----
        private sealed class URec
        {
            internal Labor Lab;
            internal BricktronDamageReceiver Dr;
            internal int Fac = int.MinValue;
            internal int Job;
            internal bool Dead;
            internal bool Seen;
            internal Vector3 Pos;
        }

        private sealed class NRec
        {
            internal AffiliationComponent Aff;
            internal int Owner = int.MinValue;
            internal float Since;
            internal bool Captured;
            internal bool Init;
        }

        private sealed class TObj
        {
            internal GameObject Go;
            internal int Fac;
            internal bool Building;
            internal string Key;
        }

        private struct PendingDeath
        {
            internal int Unit, Fac;
            internal float T;
            internal Vector3 Pos;
        }

        private struct Hit
        {
            internal int Victim, Attacker;
            internal float T;
        }

        private static bool _active, _auth;
        private static int _sceneKey;
        private static float _t0, _lastPollT, _nextPoll, _nextRes, _nextSampleT;
        private static int _sampleIdx;
        private static int _matchSeq;
        private static readonly Dictionary<int, URec> Units = new Dictionary<int, URec>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly Dictionary<int, NRec> Nests = new Dictionary<int, NRec>();
        private static readonly List<TObj> Tracked = new List<TObj>();
        private static readonly List<PendingDeath> Pending = new List<PendingDeath>();
        private static readonly Hit[] Hits = new Hit[512];
        private static int _hitPos;
        private static readonly Dictionary<int, Faction> FacById = new Dictionary<int, Faction>();
        private static readonly Dictionary<int, float> CrysAcc = new Dictionary<int, float>();
        private static readonly Dictionary<int, int> StoredE = new Dictionary<int, int>(); // purified energy waiting in the faction's crystals
        private static readonly Dictionary<int, float> IdleAcc = new Dictionary<int, float>();
        private static readonly Dictionary<int, int[]> ResNow = new Dictionary<int, int[]>();
        private static readonly Dictionary<int, int[]> ResPrev = new Dictionary<int, int[]>();
        private static bool _resBase;
        // authoritative gathering: items already counted (instance id -> object), dropped when the pooled object is released
        private static readonly Dictionary<int, GameObject> Gathered = new Dictionary<int, GameObject>();
        private static readonly List<int> GatheredGone = new List<int>();
        private static readonly Dictionary<Type, int> ResSlot = new Dictionary<Type, int>();
        private static readonly HashSet<string> LoggedKeys = new HashSet<string>();
        private static int[] _cost = new int[64];
        private static List<FireflyNest> _nestList;
        private static bool _nestListTried;

        // craft context (set by CraftingLabor.FinishRecipe prefix, read by Recipe.SpawnAll postfix)
        internal static int CraftFac = int.MinValue;
        internal static bool CraftTrack;
        internal static string CraftKey;

        // ------------------------------------------------------------------ lifecycle
        private static bool InMatch()
        {
            Scene s = SceneManager.GetActiveScene();
            if (s.buildIndex != 2) return false;
            if (BtcGameplay.InLobby) return false;
            if (!GameSignals.Received<Action>(GameSignals.LevelReady)) return false;
            return User.LocalUser != null || NetworkServer.active;
        }

        internal static float GameTime()
        {
            if (BtcReplay.Mode == BtcReplay.State.Playing) return BtcReplay.PlayClock;
            return Time.time - _t0;
        }

        private static void BeginMatch(int sceneKey)
        {
            _active = true;
            _sceneKey = sceneKey;
            _t0 = Time.time;
            _auth = NetworkServer.active && BtcReplay.Mode != BtcReplay.State.Playing;
            _matchSeq++;
            int id = (System.Environment.TickCount & 0x3FFFFFFF) ^ (_matchSeq << 20);
            if (id == 0) id = 1;
            Local.Clear(id);
            Units.Clear(); Nests.Clear(); Tracked.Clear(); Pending.Clear(); CrysAcc.Clear(); IdleAcc.Clear();
            ResNow.Clear(); ResPrev.Clear(); _resBase = false; Gathered.Clear();
            Array.Clear(Hits, 0, Hits.Length); _hitPos = 0;
            _sampleIdx = 0;
            _lastPollT = 0f;
            _nextPoll = 0f;
            _nextRes = 0f;
            _nextUnitScan = 0f;
            _nextSampleT = 0f;
            CraftFac = int.MinValue;
            if (!RemoteFresh) { Remote.Clear(0); LastRemote = 0f; }
            BtcMatchStatsNet.OnMatchReset();
            BtcLog.Info("MatchStats", "match start id=" + id + " auth=" + _auth + " replay=" + (BtcReplay.Mode == BtcReplay.State.Playing));
        }

        /// <summary>A replay starts playing: drop stats of the previous replay/match (recorded stats arrive from the file).</summary>
        internal static void OnReplayStart()
        {
            Remote.Clear(0);
            LastRemote = 0f;
        }

        /// <summary>Replay in-scene rewind destroys all objects: start over (called from BtcReplay.RewindResetWorld).</summary>
        internal static void ResetForRewind()
        {
            try { if (_active) { _active = false; BtcLog.Info("MatchStats", "replay rewind: reset"); } }
            catch (Exception e) { BtcLog.Error("MatchStats", "rewind: " + e); }
        }

        /// <summary>Called every frame (ResourcePackManager.Update).</summary>
        internal static void Tick()
        {
            BtcReplayNames.Tick();
            // Stats/Enabled off: no collecting at all (hooks check _active), no host stream, nothing recorded into replays
            if (BtcConfig.StatsEnabled != null && !BtcConfig.StatsEnabled.Value)
            {
                if (_active) { _active = false; BtcLog.Info("MatchStats", "disabled by config"); }
                return;
            }
            try
            {
                if (!InMatch())
                {
                    if (_active) { _active = false; LogSummary("end"); BtcLog.Info("MatchStats", "match end (samples=" + _sampleIdx + ")"); }
                    return;
                }
                Scene s = SceneManager.GetActiveScene();
                int key = s.GetHashCode();
                if (!_active || key != _sceneKey) BeginMatch(key);
                float now = Time.unscaledTime;
                float gt = GameTime();
                // only the host collects; clients and replay playback just show the host stream / recorded stats (user 2026-11-03)
                if (!_auth) return;
                if (now >= _nextPoll)
                {
                    _nextPoll = now + 1f;
                    float dt = Mathf.Clamp(gt - _lastPollT, 0f, 5f);
                    _lastPollT = gt;
                    RefreshFactions();
                    PollUnits(dt);
                    PollNests(dt, gt);
                    PollTracked();
                    ResolveTrades(gt);
                    if (now >= _nextRes) { _nextRes = now + 5f; PollStockpiles(); }
                    Finish();
                    Local.T = gt;
                    Local.Version++;
                }
                if (gt >= _nextSampleT)
                {
                    _nextSampleT = gt + SampleEvery;
                    Sample(gt);
                    if (_sampleIdx % 12 == 1 && _sampleIdx > 1) LogSummary("t=" + (int)gt);
                }
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "tick: " + e); _nextPoll = Time.unscaledTime + 5f; }
            BtcMatchStatsNet.Tick();
        }

        // ------------------------------------------------------------------ helpers
        internal static bool Tracks(Faction f)
        {
            return f != null && !f.IsSpectator && f != Faction.DefaultNeutral && f != Faction.DefaultEnemy;
        }

        internal static Faction FactionOf(int id)
        {
            Faction f;
            if (FacById.TryGetValue(id, out f) && f != null) return f;
            List<Faction> all = Faction.GetAllFactions();
            if (all != null) for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].ID == id) return all[i];
            return null;
        }

        private static void RefreshFactions()
        {
            FacById.Clear();
            List<Faction> all = Faction.GetAllFactions();
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                Faction f = all[i];
                if (f == null || !Tracks(f)) continue;
                FacById[f.ID] = f;
                Local.Get(f.ID, true);
            }
        }

        private static int FacId(Faction f)
        {
            return Tracks(f) ? f.ID : int.MinValue;
        }

        private static bool Enemies(int a, int b)
        {
            if (a == b) return false;
            Faction fa = FactionOf(a), fb = FactionOf(b);
            if (fa == null || fb == null) return true;
            return !fa.AllyFactions.Contains(fb);
        }

        private static int OwnFactionId()
        {
            User u = User.LocalUser;
            return u != null && u.faction != null ? u.faction.ID : int.MinValue;
        }

        // ------------------------------------------------------------------ units
        private static void PollUnits(float dt)
        {
            // FindObjectsOfType walks every object of the scene (hitch + garbage): full scan only every few seconds, the known units in between
            LabTmp.Clear();
            float uNow = Time.unscaledTime;
            if (uNow >= _nextUnitScan)
            {
                _nextUnitScan = uNow + UnitScanEvery;
                LabTmp.AddRange(UnityEngine.Object.FindObjectsOfType<Labor>());
            }
            else
            {
                foreach (KeyValuePair<int, URec> kv in Units)
                {
                    Labor l = kv.Value.Lab;
                    if (l != null && l.gameObject.activeInHierarchy) LabTmp.Add(l);
                }
            }
            List<Labor> all = LabTmp;
            foreach (KeyValuePair<int, URec> kv in Units) kv.Value.Seen = false;
            for (int i = 0; i < all.Count; i++)
            {
                Labor lab = all[i];
                if (lab == null) continue;
                int id = lab.gameObject.GetInstanceID();
                URec r;
                if (!Units.TryGetValue(id, out r))
                {
                    r = new URec { Lab = lab, Dr = lab.GetComponent<BricktronDamageReceiver>() };
                    Units[id] = r;
                }
                r.Seen = true;
                int fac = FacId(lab.faction);
                float hp = r.Dr != null ? r.Dr.CurrentHP : 1f;
                try { if (lab.Occupation != null) r.Job = JobSlot(lab.Occupation.CurrentJob); } catch { }
                r.Pos = lab.transform.position;
                if (fac == int.MinValue) { r.Fac = fac; continue; }
                if (r.Fac != fac)
                {
                    // new unit, or it changed faction (conversion is not a death)
                    r.Fac = fac;
                    if (hp > 0f) { r.Dead = false; Local.Get(fac, true).V[V_OWNED]++; }
                    else r.Dead = true;
                }
                else if (r.Dead && hp > 0f)
                {
                    // pooled object reused / revived: a new life
                    r.Dead = false;
                    Local.Get(fac, true).V[V_OWNED]++;
                }
                if (!r.Dead && hp <= 0f && !_auth) Death(r, id, GameTime());
                if (_auth && !r.Dead && r.Job == 0 && dt > 0f)
                {
                    try
                    {
                        if (lab.Autonomy != null && lab.Autonomy.Id == AutonomyStatus.Ids.freeAgent && (lab.Activity == Activity.Idle || lab.Activity == Activity.Chilling))
                        {
                            float a; IdleAcc.TryGetValue(fac, out a); IdleAcc[fac] = a + dt;
                        }
                    }
                    catch { }
                }
            }
            // vanished units: on a client an own unit that disappears died (enemy units also vanish in fog, so only HP<=0 counts for them)
            Gone.Clear();
            int own = OwnFactionId();
            bool replay = BtcReplay.Mode == BtcReplay.State.Playing;
            foreach (KeyValuePair<int, URec> kv in Units)
            {
                if (kv.Value.Seen) continue;
                URec r = kv.Value;
                if (!_auth && !r.Dead && r.Fac != int.MinValue && (r.Fac == own || replay)) Death(r, kv.Key, GameTime());
                Gone.Add(kv.Key);
            }
            for (int i = 0; i < Gone.Count; i++) Units.Remove(Gone[i]);
        }

        private static void Death(URec r, int unitId, float t)
        {
            if (r.Dead || r.Fac == int.MinValue) return;
            r.Dead = true;
            BtcMsFaction f = Local.Get(r.Fac, true);
            f.V[V_LOSS]++;
            f.V[V_PROF0 + Mathf.Clamp(r.Job, 0, NPROF - 1)]++;
            MatchTrade(unitId, r.Fac, t, r.Pos);
        }

        /// <summary>Authoritative death event (BricktronDamageReceiver._Kill postfix).</summary>
        internal static void OnKill(BricktronDamageReceiver dr, GameObject attacker)
        {
            if (!_active || !_auth || dr == null) return;
            Labor lab = dr.GetComponent<Labor>();
            if (lab == null) return;
            int id = lab.gameObject.GetInstanceID();
            URec r;
            if (!Units.TryGetValue(id, out r))
            {
                r = new URec { Lab = lab, Dr = dr, Seen = true };
                Units[id] = r;
                r.Fac = FacId(lab.faction);
                if (r.Fac != int.MinValue) Local.Get(r.Fac, true).V[V_OWNED]++;
            }
            try { if (lab.Occupation != null) r.Job = JobSlot(lab.Occupation.CurrentJob); } catch { }
            r.Pos = lab.transform.position;
            Death(r, id, GameTime());
        }

        /// <summary>Damage involvement for the trade rule (BricktronDamageReceiver.ApplyDamage postfix).</summary>
        internal static void OnHit(BricktronDamageReceiver dr, GameObject attacker)
        {
            if (!_active || !_auth || dr == null || attacker == null) return;
            Hit h;
            h.Victim = dr.gameObject.GetInstanceID();
            Labor a = attacker.GetComponent<Labor>();
            h.Attacker = a != null ? a.gameObject.GetInstanceID() : attacker.GetInstanceID();
            h.T = GameTime();
            Hits[_hitPos] = h;
            _hitPos = (_hitPos + 1) % Hits.Length;
        }

        private static bool Involved(int a, int b, float t)
        {
            for (int i = 0; i < Hits.Length; i++)
            {
                Hit h = Hits[i];
                if (h.T <= 0f || t - h.T > InvolveWindow) continue;
                if ((h.Victim == a && h.Attacker == b) || (h.Victim == b && h.Attacker == a)) return true;
            }
            return false;
        }

        // Trade rule (docs/decisions.md): a death is a trade when an enemy unit died within 5 s that was involved with it (damage either way
        // during the last 10 s) or died within 15 voxels; deaths are paired one-to-one; unpaired after 5 s = free.
        private static void MatchTrade(int unit, int fac, float t, Vector3 pos)
        {
            int best = -1;
            bool bestInv = false;
            for (int i = 0; i < Pending.Count; i++)
            {
                PendingDeath p = Pending[i];
                if (!Enemies(fac, p.Fac) || Mathf.Abs(t - p.T) > TradeWindow) continue;
                bool inv = Involved(unit, p.Unit, t);
                bool near = (pos - p.Pos).sqrMagnitude <= TradeRadius * TradeRadius;
                if (!inv && !near) continue;
                if (best < 0 || (inv && !bestInv)) { best = i; bestInv = inv; }
            }
            if (best >= 0)
            {
                Local.Get(fac, true).V[V_TRADE]++;
                Local.Get(Pending[best].Fac, true).V[V_TRADE]++;
                Pending.RemoveAt(best);
                return;
            }
            Pending.Add(new PendingDeath { Unit = unit, Fac = fac, T = t, Pos = pos });
        }

        private static void ResolveTrades(float t)
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (t - Pending[i].T <= TradeWindow) continue;
                Local.Get(Pending[i].Fac, true).V[V_FREE]++;
                Pending.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ crystals
        private static List<FireflyNest> NestList()
        {
            if (!_nestListTried)
            {
                _nestListTried = true;
                try
                {
                    FieldInfo fi = typeof(FireflyNest).GetField("FireflyNests", BindingFlags.NonPublic | BindingFlags.Static);
                    _nestList = fi != null ? fi.GetValue(null) as List<FireflyNest> : null;
                }
                catch (Exception e) { BtcLog.Warn("MatchStats", "nest list: " + e.Message); }
            }
            return _nestList;
        }

        private static readonly HashSet<int> InNest = new HashSet<int>();
        private static readonly List<FireflyNest> NestTmp = new List<FireflyNest>();

        private static void PollNests(float dt, float t)
        {
            NestTmp.Clear();
            List<FireflyNest> l = NestList();
            if (l != null && l.Count > 0) NestTmp.AddRange(l);
            else NestTmp.AddRange(UnityEngine.Object.FindObjectsOfType<FireflyNest>());
            for (int i = 0; i < Local.Factions.Count; i++) Local.Factions[i].V[V_CRYS] = 0;
            InNest.Clear();
            StoredE.Clear();
            for (int i = 0; i < NestTmp.Count; i++)
            {
                FireflyNest n = NestTmp[i];
                if (n == null) continue;
                int id = n.gameObject.GetInstanceID();
                NRec r;
                if (!Nests.TryGetValue(id, out r)) { r = new NRec { Aff = n.GetComponent<AffiliationComponent>() }; Nests[id] = r; }
                if (r.Aff == null) continue;
                int owner = FacId(r.Aff.faction);
                if (!r.Init)
                {
                    // initial owner (home crystal / pre-owned): not a capture
                    r.Init = true;
                    r.Owner = owner;
                    r.Since = t;
                    r.Captured = false;
                }
                else if (owner != r.Owner)
                {
                    if (r.Owner != int.MinValue && r.Captured)
                    {
                        BtcMsFaction lost = Local.Get(r.Owner, true);
                        lost.V[V_HOLDSUM] += Mathf.RoundToInt(t - r.Since);
                        lost.V[V_HOLDN]++;
                    }
                    r.Captured = owner != int.MinValue;
                    r.Owner = owner;
                    r.Since = t;
                }
                if (owner == int.MinValue) continue;
                BtcMsFaction f = Local.Get(owner, true);
                f.V[V_CRYS]++;
                float a; CrysAcc.TryGetValue(owner, out a); CrysAcc[owner] = a + dt;
                int se = 0;
                try
                {
                    foreach (IDescriptor d in n.mainRecepteur.StoredItems)
                    {
                        GameObject go = d.GameObject;
                        if (go == null || !go.CompareTag("Firefly")) continue;
                        Firefly ff = go.GetComponent<Firefly>();
                        if (ff == null) continue;
                        InNest.Add(ff.GetInstanceID());
                        se += ff.pureEnergy + ff.storedEnergy;   // storedEnergy: dead unit waiting in the nest to respawn
                    }
                }
                catch { }
                if (se > 0) { int had; StoredE.TryGetValue(owner, out had); StoredE[owner] = had + se; }
            }
            // souls flying to a nest (dead unit: pure energy after the death penalty) are energy of the faction too
            try
            {
                Firefly[] all = UnityEngine.Object.FindObjectsOfType<Firefly>();
                for (int i = 0; i < all.Length; i++)
                {
                    Firefly ff = all[i];
                    if (ff == null || InNest.Contains(ff.GetInstanceID()) || ff.GetComponent<Labor>() != null) continue;
                    int fo = FacId(ff.faction);
                    int en = ff.pureEnergy + ff.storedEnergy;
                    if (fo == int.MinValue || en <= 0) continue;
                    int had2; StoredE.TryGetValue(fo, out had2); StoredE[fo] = had2 + en;
                }
            }
            catch (Exception ex) { BtcLog.Warn("MatchStats", "souls: " + ex.Message); }
        }

        // ------------------------------------------------------------------ stockpiles (gathered = net increase of stockpile content)
        private static int SlotOf(Type t)
        {
            int s;
            if (ResSlot.TryGetValue(t, out s)) return s;
            s = Array.IndexOf(ResTypes, t.Name);
            ResSlot[t] = s;
            return s;
        }

        private const float UnitScanEvery = 4f;
        private static float _nextUnitScan;
        private static readonly List<Labor> LabTmp = new List<Labor>();

        private static void PollStockpiles()
        {
            foreach (KeyValuePair<int, int[]> kv in ResNow) Array.Clear(kv.Value, 0, NRES);
            Recepteur[] all = UnityEngine.Object.FindObjectsOfType<Recepteur>();
            for (int i = 0; i < all.Length; i++)
            {
                Recepteur r = all[i];
                if (r == null) continue;
                try
                {
                    if (r.AssetKey != ObjetsDynamiques.Palette) continue;
                    int fac = FacId(r.faction);
                    if (fac == int.MinValue) continue;
                    Description d = r.ContentDescription;
                    if (d == null || d.DicoAdjectif == null) continue;
                    int[] now;
                    if (!ResNow.TryGetValue(fac, out now)) { now = new int[NRES]; ResNow[fac] = now; }
                    foreach (KeyValuePair<Type, Adjectif> kv in d.DicoAdjectif)
                    {
                        if (kv.Key == null) continue;
                        int s = SlotOf(kv.Key);
                        if (s < 0) continue;
                        Quantite q = kv.Value as Quantite;
                        now[s] += q != null ? q.valeur : 1;
                    }
                }
                catch { }
            }
            foreach (KeyValuePair<int, int[]> kv in ResNow)
            {
                int[] prev;
                if (!ResPrev.TryGetValue(kv.Key, out prev)) { prev = new int[NRES]; ResPrev[kv.Key] = prev; }
                // authoritative: gathering is counted per item in OnStored; the stockpile delta is the light-mode fallback
                if (_resBase && !_auth)
                {
                    BtcMsFaction f = Local.Get(kv.Key, true);
                    for (int s = 0; s < NRES; s++)
                    {
                        int d = kv.Value[s] - prev[s];
                        if (d <= 0) continue;
                        f.V[V_RES0 + s] += d;
                        f.V[V_SCORE] += d * RawMs[s];
                    }
                }
                Array.Copy(kv.Value, prev, NRES);
            }
            _resBase = true;
            // forget released (pooled) items so a reused object counts again
            GatheredGone.Clear();
            foreach (KeyValuePair<int, GameObject> kv in Gathered)
            {
                bool gone;
                try { gone = kv.Value.IsNullOrReleased(); } catch { gone = true; }
                if (gone) GatheredGone.Add(kv.Key);
            }
            for (int i = 0; i < GatheredGone.Count; i++) Gathered.Remove(GatheredGone[i]);
        }

        /// <summary>
        /// Recepteur.AddObject postfix (authoritative): an item counts as gathered by a faction the first time in its life it enters a
        /// recepteur of that faction (a bricktron's hands or a stockpile). Items present before the first stockpile poll (start
        /// resources) are only marked. Crafted outputs count too (same as the stockpile view: "gathered" = new stock).
        /// </summary>
        internal static void OnStored(Recepteur r, GameObject item)
        {
            if (!_active || !_auth || r == null || item == null) return;
            int id = item.GetInstanceID();
            if (Gathered.ContainsKey(id)) return;
            int fac = FacId(r.faction);
            if (fac == int.MinValue) return;
            IDescriptor d = item.GetComponent<IDescriptor>();
            Ressource p = d != null ? d.Principale : null;
            if (p == null) return;
            Gathered[id] = item;
            if (!_resBase) return;
            int s = SlotOf(p.GetType());
            if (s < 0) return;
            BtcMsFaction f = Local.Get(fac, true);
            f.V[V_RES0 + s]++;
            f.V[V_SCORE] += RawMs[s];
        }

        /// <summary>Display name of an asset key: "CaptureWard" / "Wood_SingleLog" -> "Capture Ward" / "Wood Single Log".</summary>
        private static readonly Dictionary<string, string> PrettyCache = new Dictionary<string, string>();
        internal static string Pretty(string key)
        {
            if (string.IsNullOrEmpty(key)) return "?";
            string r;
            if (PrettyCache.TryGetValue(key, out r)) return r;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(key.Length + 8);
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '_') { if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && char.IsLower(key[i - 1]) && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                sb.Append(c);
            }
            r = sb.ToString().Trim();
            if (r.Length > 28) r = r.Substring(0, 28);
            if (PrettyCache.Count < 512) PrettyCache[key] = r;
            return r;
        }

        // ------------------------------------------------------------------ crafting / building (authoritative only)
        private static readonly string[] Materials = { "plank", "ingot", "stoneblock", "brick", "glass", "cog", "rope", "fabric", "terracotta", "spring", "stick", "purified" };
        private static readonly string[] Deployables = { "bomb", "trap", "barrel", "ward", "lantern" };

        private static bool Has(string name, string[] subs)
        {
            for (int i = 0; i < subs.Length; i++) if (name.IndexOf(subs[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool IsMaterial(string name)
        {
            return Has(name, Materials) || string.Equals(name, "Iron", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Crafting-only bricktron-time of one recipe completion (table "Craft" column, else work*475 + heat*718 ms).</summary>
        internal static int CraftMs(RecipeInfo info)
        {
            string n = info.Creates != null ? (info.Creates.Name ?? string.Empty) : string.Empty;
            if (n.IndexOf("bomb", StringComparison.OrdinalIgnoreCase) >= 0) return 1583;
            if (n.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0) return 2850;
            if (Has(n, Deployables)) return 4750;
            return Mathf.Max(0, info.work) * 475 + Mathf.Max(0, info.heat) * 718;
        }

        internal static void OnCraftBegin(CraftingLabor cl, Recipe recipe)
        {
            CraftFac = int.MinValue;
            CraftTrack = false;
            if (!_active || !_auth || cl == null || recipe == null || recipe.Info == null) return;
            int fac = FacId(Faction.GetFaction(cl.gameObject));
            if (fac == int.MinValue) return;
            RecipeInfo info = recipe.Info;
            string n = info.Creates != null ? (info.Creates.Name ?? string.Empty) : string.Empty;
            BtcMsFaction f = Local.Get(fac, true);
            f.V[V_SCORE] += CraftMs(info);
            if (!IsMaterial(n)) { int q = Mathf.Max(1, info.QtyCreated); f.V[V_CRAFT] += q; f.AddDet(D_CRAFT, Pretty(n), q); }
            CraftKey = Pretty(n);
            CraftFac = fac;
            CraftTrack = false; // "items lost" removed (user 2026-11-03); slot V_ILOST stays in the wire layout
            LogKey("craft", n);
        }


        /// <summary>ExplodeOnDeath.Explode prefix (authoritative): an explosive barrel blew up (any cause); counted for the barrel's faction.</summary>
        internal static void OnExplode(GameObject go)
        {
            if (!_active || !_auth || go == null) return;
            IDescriptor d = go.GetComponent<IDescriptor>();
            if (d == null || d.AssetKey == null || d.AssetKey.Name == null || d.AssetKey.Name.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) < 0) return;
            int fac = FacId(Faction.GetFaction(go));
            if (fac == int.MinValue) return;
            Local.Get(fac, true).V[V_BARREL]++;
            LogKey("barrel", d.AssetKey.Name);
        }
        internal static void OnCraftEnd() { CraftFac = int.MinValue; CraftTrack = false; }

        /// <summary>Recipe.SpawnAll postfix while a CraftingLabor recipe finishes: remember deployable items for "items lost".</summary>
        internal static void OnCraftSpawned(List<GameObject> gos)
        {
            if (CraftFac == int.MinValue || !CraftTrack || gos == null) return;
            for (int i = 0; i < gos.Count; i++) if (gos[i] != null && Tracked.Count < 4000) Tracked.Add(new TObj { Go = gos[i], Fac = CraftFac, Key = CraftKey });
        }

        internal static void OnBuilt(Blueprint bp, GameObject result)
        {
            if (!_active || !_auth || bp == null || result == null || bp.IsAnti) return;
            int fac = FacId(Faction.GetFaction(result));
            if (fac == int.MinValue) return;
            string key = "?";
            try { if (bp.BlueprintResult != null) key = Pretty(bp.BlueprintResult.Name); } catch { }
            BtcMsFaction bf = Local.Get(fac, true);
            bf.V[V_BUILT]++;
            bf.AddDet(D_BUILT, key, 1);
            if (Tracked.Count < 4000) Tracked.Add(new TObj { Go = result, Fac = fac, Building = true, Key = key });
            try { LogKey("built", bp.BlueprintResult != null ? bp.BlueprintResult.Factory + "." + bp.BlueprintResult.Name : "?"); } catch { }
        }

        private static void LogKey(string what, string key)
        {
            if (LoggedKeys.Count < 64 && LoggedKeys.Add(what + ":" + key)) BtcLog.Info("MatchStats", what + " key=" + key);
        }

        private static void PollTracked()
        {
            for (int i = Tracked.Count - 1; i >= 0; i--)
            {
                TObj o = Tracked[i];
                bool gone;
                try { gone = o.Go.IsNullOrReleased(); } catch { gone = true; }
                if (!gone) continue;
                BtcMsFaction lf = Local.Get(o.Fac, true);
                lf.V[o.Building ? V_BLOST : V_ILOST]++;
                lf.AddDet(o.Building ? D_BLOST : D_ILOST, o.Key ?? "?", 1);
                Tracked.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ derived values + sampling
        private static int Cost(int k)
        {
            try
            {
                GeneralRules g = GeneralRules.Main;
                if (g != null) return g.GetHomeCrystalSpawnRequiredEnergy(k);
            }
            catch { }
            return 100;
        }

        private static void Finish()
        {
            for (int i = 0; i < Local.Factions.Count; i++) { Local.Factions[i].V[V_ALIVE] = 0; Local.Factions[i].V[V_AUTH] = _auth ? 1 : 0; }
            foreach (KeyValuePair<int, URec> kv in Units)
            {
                URec r = kv.Value;
                if (r.Dead || r.Fac == int.MinValue) continue;
                Local.Get(r.Fac, true).V[V_ALIVE]++;
            }
            int maxN = 0;
            for (int i = 0; i < Local.Factions.Count; i++) maxN = Mathf.Max(maxN, Local.Factions[i].V[V_ALIVE]);
            if (_cost.Length < maxN) _cost = new int[Mathf.NextPowerOfTwo(maxN)];
            for (int k = 0; k < maxN; k++) _cost[k] = Cost(k);
            for (int i = 0; i < Local.Factions.Count; i++)
            {
                BtcMsFaction f = Local.Factions[i];
                int e = 0;
                for (int k = 0; k < f.V[V_ALIVE]; k++) e += _cost[k];
                int se;
                if (StoredE.TryGetValue(f.Id, out se)) e += se;
                f.V[V_ENERGY] = e;
                float a;
                if (CrysAcc.TryGetValue(f.Id, out a)) f.V[V_CRYSEC] = (int)a;
                if (IdleAcc.TryGetValue(f.Id, out a)) f.V[V_IDLE] = (int)a;
            }
        }

        /// <summary>One log line per faction (every 60 s of match time and at match end): "[MatchStats] sum ...".</summary>
        private static void LogSummary(string when)
        {
            for (int i = 0; i < Local.Factions.Count; i++)
            {
                BtcMsFaction f = Local.Factions[i];
                int[] v = f.V;
                if (v[V_OWNED] == 0 && v[V_CRYSEC] == 0) continue;
                System.Text.StringBuilder sb = new System.Text.StringBuilder(160);
                sb.Append("sum ").Append(when).Append(" fac=").Append(f.Id).Append(" auth=").Append(v[V_AUTH])
                  .Append(" alive=").Append(v[V_ALIVE]).Append(" owned=").Append(v[V_OWNED]).Append(" loss=").Append(v[V_LOSS])
                  .Append(" energy=").Append(v[V_ENERGY]).Append(" crys=").Append(v[V_CRYS]).Append(" crysSec=").Append(v[V_CRYSEC])
                  .Append(" hold=").Append(v[V_HOLDSUM]).Append('/').Append(v[V_HOLDN]).Append(" idle=").Append(v[V_IDLE])
                  .Append(" trade=").Append(v[V_TRADE]).Append(" free=").Append(v[V_FREE]).Append(" craft=").Append(v[V_CRAFT])
                  .Append(" built=").Append(v[V_BUILT]).Append(" ilost=").Append(v[V_ILOST]).Append(" blost=").Append(v[V_BLOST])
                  .Append(" score=").Append(v[V_SCORE]).Append(" prof=");
                for (int k = 0; k < NPROF; k++) { if (k > 0) sb.Append(','); sb.Append(v[V_PROF0 + k]); }
                sb.Append(" res=");
                for (int k = 0; k < NRES; k++) { if (k > 0) sb.Append(','); sb.Append(v[V_RES0 + k]); }
                BtcLog.Info("MatchStats", sb.ToString());
            }
        }

        private static void Sample(float t)
        {
            int idx = _sampleIdx++;
            for (int i = 0; i < Local.Factions.Count; i++)
            {
                BtcMsFaction f = Local.Factions[i];
                int[] v = new int[NV];
                Array.Copy(f.V, v, NV);
                f.AddSample(idx, t, v);
            }
        }
    }
}
