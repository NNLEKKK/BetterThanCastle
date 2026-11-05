using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brix.Components;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Input;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Game.Semantique;
using Brix.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// <summary>
    /// Automatic replay timeline marks (recorded by the host/single player into the reserved "tl=" header region, nothing else is written):
    /// D = bricktron deaths (merged per colour, with a count), C = crystal capture / steal, X = barrel explosion, F = large fight
    /// (>=2 enemy sides with >=50% of their living units in combat). Z (pauses) is drawn from the BtcTimer events in the same style.
    /// Playback: icons in a lane above the replay panel, per-kind hide chips, Alt + hover over the timeline = mini map preview from snapshots that
    /// are collected while the replay plays (not stored anywhere), combat highlights on the game's mini map.
    /// Local only: no wire traffic.
    /// </summary>
    internal static class BtcAutoMarks
    {
        internal struct AEv
        {
            internal char K;
            internal float T, A;
            internal int N;
            internal string C1, C2;
            internal float PX, PY, PZ;
            internal bool HasPos;
        }

        private struct RawDeath { internal float T; internal string Col; internal Vector3 P; }

        private sealed class Snap
        {
            internal float T;
            internal short[] Ux, Uz;
            internal byte[] Uf;     // faction index | 0x80 engaged
            internal short[] Cx, Cz;
            internal byte[] Co;     // faction index, 255 = neutral
        }

        private sealed class Item
        {
            internal char K;
            internal float X, T, A;
            internal int N, Row;
            internal string C1, C2;
            internal float PX, PY, PZ;
            internal bool HasPos;
        }

        private const float DeathGap = 6f;
        private const float EngagedWindow = 6f;
        private const float FightCloseAfter = 4f;
        private const float SnapEvery = 2f;
        private const int CellSize = 18;
        private const int MaxAuto = 400;

        private static readonly HashSet<string> _loggedKeys = new HashSet<string>();
        private static readonly List<RawDeath> Deaths = new List<RawDeath>();
        private static readonly List<AEv> RecEv = new List<AEv>();
        internal static readonly List<AEv> Play = new List<AEv>();
        internal static readonly HashSet<char> HiddenKinds = new HashSet<char>();
        private static readonly Dictionary<int, float> LastHit = new Dictionary<int, float>();
        private static readonly Dictionary<int, Snap> Snaps = new Dictionary<int, Snap>();
        private static readonly List<int> SnapFacIds = new List<int>();
        private static float _snapFrontier, _snapTotal = -1f;

        private static bool _hiddenLoaded;
        private static float _next, _nextList, _lastRecT;
        private static readonly List<Labor> Labors = new List<Labor>();
        private static readonly List<Labor> _walked = new List<Labor>();
        private static float _nextWalk, _nextNestWalk;
        private static readonly Dictionary<int, int> ArmyAlive = new Dictionary<int, int>(), ArmyEng = new Dictionary<int, int>();
        private static readonly List<FireflyNest> Nests = new List<FireflyNest>();
        private sealed class NestMem { internal FireflyNest N; internal int Owner; internal bool Home; internal Vector3 P; }
        private static readonly Dictionary<int, NestMem> NestMems = new Dictionary<int, NestMem>();
        private static readonly List<int> NestGone = new List<int>();
        private static readonly Dictionary<int, float> LastDeath = new Dictionary<int, float>();
        private static readonly Dictionary<int, AffiliationComponent> NestAff = new Dictionary<int, AffiliationComponent>();
        private static readonly Dictionary<int, int> Alive = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> Engaged = new Dictionary<int, int>();
        private static readonly List<float> EX = new List<float>(), EZ = new List<float>();
        private static readonly List<int> EF = new List<int>();

        // fight state
        private static bool _fOpen;
        private static float _fStartRec, _fLastOk;
        private static int _fPeak, _fSideA = int.MinValue, _fSideB = int.MinValue;
        private static Vector3 _fPos;

        // ------------------------------------------------------------------ helpers
        private static bool Rec { get { return BtcReplay.Mode == BtcReplay.State.Recording; } }
        private static bool Playing { get { return BtcReplay.Mode == BtcReplay.State.Playing; } }

        private static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

        private static string FacHex(int id)
        {
            Faction f = BtcMatchStats.FactionOf(id);
            return f != null ? Hex(f.FactionColor) : "AAAAAA";
        }

        private static Color ParseCol(string hex)
        {
            Color c;
            if (hex != null && hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out c)) return c;
            return new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        // ------------------------------------------------------------------ recording hooks
        internal static void OnRecordStart()
        {
            Deaths.Clear();
            RecEv.Clear();
            NestMems.Clear();
            LastDeath.Clear();
            _fOpen = false;
            _lastRecT = 0f;
        }

        internal static void OnKill(BricktronDamageReceiver dr)
        {
            if (!Rec || dr == null || !BtcConfig.ReplayAutoMarks.Value) return;
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            Labor lab = dr.GetComponent<Labor>();
            if (lab == null || !BtcMatchStats.Tracks(lab.faction)) return;
            // _Kill runs again for every hit on a dying unit: one death per unit instance within 20 s
            int uid = lab.gameObject.GetInstanceID();
            float last;
            if (LastDeath.TryGetValue(uid, out last) && t - last < 20f && t >= last) return;
            LastDeath[uid] = t;
            Deaths.Add(new RawDeath { T = t, Col = Hex(lab.faction.FactionColor), P = lab.transform.position });
        }

        internal static void OnHit(BricktronDamageReceiver dr, GameObject attacker)
        {
            if (dr == null || attacker == null) return;
            if (!Rec && !Playing) return;
            float now = Time.time;
            LastHit[dr.gameObject.GetInstanceID()] = now;
            Labor a = attacker.GetComponent<Labor>();
            if (a != null) LastHit[a.gameObject.GetInstanceID()] = now;
        }

        internal static void OnBarrel(GameObject go)
        {
            if (!Rec || go == null || !BtcConfig.ReplayAutoMarks.Value) return;
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            IDescriptor d = go.GetComponent<IDescriptor>();
            string key = d != null && d.AssetKey != null ? d.AssetKey.Name : null;
            bool barrel = Apparence.Is<ExplosiveBarrel>(go) || (key != null && key.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) >= 0);
            if (_loggedKeys.Add(key + "|" + barrel)) BtcLog.Info("AutoMarks", "explosif key=" + key + " barrel=" + barrel);
            if (!barrel) return;
            Faction f = Faction.GetFaction(go);
            string col = BtcMatchStats.Tracks(f) ? Hex(f.FactionColor) : "AAAAAA";
            Vector3 bp = go.transform.position;
            RecEv.Add(new AEv { K = 'X', T = t, C1 = col, PX = bp.x, PY = bp.y, PZ = bp.z, HasPos = true });
        }

        // ------------------------------------------------------------------ payload (tl= region)
        private static string Pos(AEv e)
        {
            if (!e.HasPos) return "";
            return "," + Mathf.RoundToInt(e.PX) + "," + Mathf.RoundToInt(e.PY) + "," + Mathf.RoundToInt(e.PZ);
        }

        private static void ReadPos(ref AEv e, string[] p, int at)
        {
            if (p.Length < at + 3) return;
            float x, y, z;
            if (float.TryParse(p[at], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) && float.TryParse(p[at + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            { e.PX = x; e.PY = y; e.PZ = z; e.HasPos = true; }
        }

        /// <summary>Auto events as "K,..." tokens joined with ';' (deaths merged), at most max chars.</summary>
        internal static string Payload(int max)
        {
            if (_fOpen) CloseFight(Mathf.Max(_fStartRec, BtcReplay.RecTime >= 0f ? BtcReplay.RecTime : _lastRecT));
            CultureInfo c = CultureInfo.InvariantCulture;
            List<AEv> all = new List<AEv>(RecEv);
            // merge deaths per colour: chain with gap <= DeathGap
            Dictionary<string, int> open = new Dictionary<string, int>();
            for (int i = 0; i < Deaths.Count; i++)
            {
                RawDeath d = Deaths[i];
                int idx;
                if (open.TryGetValue(d.Col, out idx) && d.T - all[idx].A <= DeathGap)
                {
                    AEv e = all[idx]; e.N++; e.A = d.T; all[idx] = e;
                }
                else
                {
                    all.Add(new AEv { K = 'D', T = d.T, A = d.T, N = 1, C1 = d.Col, PX = d.P.x, PY = d.P.y, PZ = d.P.z, HasPos = true });
                    open[d.Col] = all.Count - 1;
                }
            }
            all.Sort(delegate (AEv a, AEv b) { return a.T.CompareTo(b.T); });
            StringBuilder sb = new StringBuilder(512);
            int cnt = 0;
            for (int i = 0; i < all.Count && cnt < MaxAuto; i++)
            {
                AEv e = all[i];
                string s;
                switch (e.K)
                {
                    case 'D': s = "D," + e.T.ToString("F1", c) + "," + e.C1 + "," + e.N + Pos(e); break;
                    case 'C':
                    case 'N':
                    case 'H': s = e.K + "," + e.T.ToString("F1", c) + "," + e.C1 + "," + (e.C2 ?? "-") + Pos(e); break;
                    case 'X': s = "X," + e.T.ToString("F1", c) + "," + e.C1 + Pos(e); break;
                    case 'W':
                    case 'F': s = e.K + "," + e.T.ToString("F1", c) + "," + e.A.ToString("F1", c) + "," + e.N + "," + (e.C1 ?? "-") + "," + (e.C2 ?? "-") + Pos(e); break;
                    default: continue;
                }
                if (sb.Length + s.Length + 1 > max) break;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(s);
                cnt++;
            }
            if (cnt > 0 || Deaths.Count > 0) BtcLog.Info("AutoMarks", "payload: " + cnt + " events (" + Deaths.Count + " raw deaths, " + RecEv.Count + " other)");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ playback load
        internal static void ClearPlay() { Play.Clear(); }

        /// <summary>BtcTimer.LoadTl token: true when it was an auto event (handled here).</summary>
        internal static bool TryParse(string[] p, CultureInfo c)
        {
            if (p.Length < 3) return false;
            char k = p[0].Length == 1 ? p[0][0] : ' ';
            if (k == 'W') return true; // skirmish marks were dropped
            AEv e = new AEv { K = k };
            switch (k)
            {
                case 'D': if (p.Length < 4) return true; e.T = float.Parse(p[1], c); e.A = e.T; e.C1 = p[2]; e.N = int.Parse(p[3], c); ReadPos(ref e, p, 4); break;
                case 'C':
                case 'N':
                case 'H': if (p.Length < 4) return true; e.T = float.Parse(p[1], c); e.C1 = p[2]; e.C2 = p[3] == "-" ? null : p[3]; e.N = 1; ReadPos(ref e, p, 4); break;
                case 'X': e.T = float.Parse(p[1], c); e.C1 = p[2]; e.N = 1; ReadPos(ref e, p, 3); break;
                case 'W':
                case 'F':
                    if (p.Length < 6) return true;
                    e.T = float.Parse(p[1], c); e.A = float.Parse(p[2], c); e.N = int.Parse(p[3], c);
                    e.C1 = p[4] == "-" ? null : p[4]; e.C2 = p[5] == "-" ? null : p[5];
                    ReadPos(ref e, p, 6);
                    break;
                default: return false;
            }
            Play.Add(e);
            _layoutKey = -1;
            return true;
        }

        internal static void SortPlay()
        {
            Play.Sort(delegate (AEv a, AEv b) { return a.T.CompareTo(b.T); });
        }

        // ------------------------------------------------------------------ scan (recording + playback)
        /// <summary>Every frame (BtcTimer.Tick): cheap throttle, then a 0.5 s scan.</summary>
        internal static void Tick()
        {
            bool rec = Rec, play = Playing;
            if (!rec && !play) { if (LastHit.Count > 0) LastHit.Clear(); if (_marks.Count > 0) ClearMarks(); _fOpen = false; return; }
            try
            {
                if (play) { PulseMarks(); CameraTick(); }
                float now = Time.unscaledTime;
                if (now < _next) return;
                _next = now + 1f;
                if (rec) { float t = BtcReplay.RecTime; if (t >= 0f) _lastRecT = t; }
                Scan(rec, play);
            }
            catch (Exception e) { BtcLog.Error("AutoMarks", "tick: " + e); }
        }

        private static void RefreshLists()
        {
            Labors.Clear();
            BtcRegistry.CollectLabors(Labors);
            Nests.Clear();
            List<FireflyNest> l = BtcMatchStats.NestList();
            if (l != null) Nests.AddRange(l);
            if (Nests.Count == 0 && Time.unscaledTime >= _nextNestWalk) { _nextNestWalk = Time.unscaledTime + 10f; Nests.AddRange(UnityEngine.Object.FindObjectsOfType<FireflyNest>()); }
        }

        private static void Scan(bool rec, bool play)
        {
            float now = Time.unscaledTime;
            if (now >= _nextList) { _nextList = now + 3f; RefreshLists(); }
            float tm = Time.time;
            Alive.Clear();
            Engaged.Clear();
            ArmyAlive.Clear();
            ArmyEng.Clear();
            EX.Clear(); EZ.Clear(); EF.Clear(); EY.Clear();
            for (int i = 0; i < Labors.Count; i++)
            {
                Labor lab = Labors[i];
                if (lab == null || !lab.gameObject.activeInHierarchy) continue;
                Faction f = lab.faction;
                if (!BtcMatchStats.Tracks(f)) continue;
                BricktronDamageReceiver dr = lab.GetComponent<BricktronDamageReceiver>();
                if (dr != null && dr.CurrentHP <= 0f) continue;
                int fid = f.ID;
                int n; Alive.TryGetValue(fid, out n); Alive[fid] = n + 1;
                Occupation occ = lab.Occupation;
                bool army = occ != null && BtcMatchStats.JobSlot(occ.CurrentJob) != 0;
                if (army) { ArmyAlive.TryGetValue(fid, out n); ArmyAlive[fid] = n + 1; }
                float lh;
                if (LastHit.TryGetValue(lab.gameObject.GetInstanceID(), out lh) && tm - lh <= EngagedWindow)
                {
                    Engaged.TryGetValue(fid, out n); Engaged[fid] = n + 1;
                    if (army) { ArmyEng.TryGetValue(fid, out n); ArmyEng[fid] = n + 1; }
                    Vector3 p = lab.transform.position;
                    EX.Add(p.x); EY.Add(p.y); EZ.Add(p.z); EF.Add(fid);
                }
            }
            BuildClusters();
            bool large = EvalFight(tm, rec);
            if (rec) PollNests();
            if (play)
            {
                UpdateMinimap(large);
                TakeSnapshot();
            }
            if (LastHit.Count > 600) PruneHits(tm);
        }

        private static void PruneHits(float tm)
        {
            List<int> dead = null;
            foreach (KeyValuePair<int, float> kv in LastHit)
                if (tm - kv.Value > EngagedWindow * 3f) { if (dead == null) dead = new List<int>(); dead.Add(kv.Key); }
            if (dead != null) for (int i = 0; i < dead.Count; i++) LastHit.Remove(dead[i]);
        }

        // ------------------------------------------------------------------ fights
        private static bool EvalFight(float tm, bool rec)
        {
            int a = int.MinValue, b = int.MinValue, total = 0, ea = 0, eb = 0;
            List<int> sides = null;
            foreach (KeyValuePair<int, int> kv in ArmyEng)
            {
                int al; ArmyAlive.TryGetValue(kv.Key, out al);
                if (kv.Value >= 3 && kv.Value * 2 >= al)
                {
                    if (sides == null) sides = new List<int>();
                    sides.Add(kv.Key);
                }
            }
            bool ok = false;
            if (sides != null && sides.Count >= 2)
            {
                for (int i = 0; i < sides.Count && !ok; i++)
                    for (int j = i + 1; j < sides.Count; j++)
                        if (BtcMatchStats.Enemies(sides[i], sides[j])) { a = sides[i]; b = sides[j]; ok = true; break; }
            }
            if (ok)
            {
                ArmyEng.TryGetValue(a, out ea); ArmyEng.TryGetValue(b, out eb);
                total = ea + eb;
                _fLastOk = tm;
                if (!_fOpen)
                {
                    _fOpen = true;
                    BtcLog.Info("AutoMarks", "large fight opens: sides " + a + "/" + b + " army engaged " + ea + "/" + eb);
                    _fStartRec = rec ? BtcReplay.RecTime : 0f;
                    _fPeak = 0;
                }
                if (total >= _fPeak)
                {
                    _fPeak = total; _fSideA = a; _fSideB = b;
                    Vector3 s = Vector3.zero;
                    for (int i = 0; i < EX.Count; i++) s += new Vector3(EX[i], EY[i], EZ[i]);
                    if (EX.Count > 0) _fPos = s / EX.Count;
                }
            }
            else if (_fOpen && tm - _fLastOk > FightCloseAfter)
            {
                if (rec) CloseFight(BtcReplay.RecTime >= 0f ? BtcReplay.RecTime - FightCloseAfter : _lastRecT);
                _fOpen = false;
            }
            return ok;
        }

        private static void CloseFight(float endT)
        {
            if (_fOpen && Rec && BtcConfig.ReplayAutoMarks.Value && endT - _fStartRec >= 3f)
                RecEv.Add(new AEv { K = 'F', T = _fStartRec, A = endT, N = _fPeak, C1 = _fSideA == int.MinValue ? null : FacHex(_fSideA), C2 = _fSideB == int.MinValue ? null : FacHex(_fSideB), PX = _fPos.x, PY = _fPos.y, PZ = _fPos.z, HasPos = true });
            _fOpen = false;
        }

        // ------------------------------------------------------------------ crystals (recording)
        private static void PollNests()
        {
            if (!BtcConfig.ReplayAutoMarks.Value) return;
            float t = BtcReplay.RecTime;
            if (t < 0f) return;
            for (int i = 0; i < Nests.Count; i++)
            {
                FireflyNest n = Nests[i];
                if (n == null) continue;
                int id = n.gameObject.GetInstanceID();
                if (NestMems.ContainsKey(id)) continue;
                AffiliationComponent aff;
                if (!NestAff.TryGetValue(id, out aff) || aff == null) { aff = n.GetComponent<AffiliationComponent>(); NestAff[id] = aff; }
                if (aff == null) continue;
                NestMems[id] = new NestMem { N = n, Owner = BtcMatchStats.Tracks(aff.faction) ? aff.faction.ID : int.MinValue, Home = n.isHome, P = n.transform.position };
            }
            NestGone.Clear();
            foreach (KeyValuePair<int, NestMem> kv in NestMems)
            {
                NestMem m = kv.Value;
                int owner;
                bool destroyed = m.N == null;
                if (destroyed) owner = int.MinValue;
                else
                {
                    m.Home |= m.N.isHome;
                    m.P = m.N.transform.position;
                    AffiliationComponent aff;
                    NestAff.TryGetValue(kv.Key, out aff);
                    owner = aff != null && BtcMatchStats.Tracks(aff.faction) ? aff.faction.ID : int.MinValue;
                }
                int prev = m.Owner;
                if (destroyed) NestGone.Add(kv.Key);
                if (prev == owner) continue;
                m.Owner = owner;
                // home crystal lost (taken, or blown up): a player is out. Other crystals: taken over (C), or neutralised (N)
                if (prev != int.MinValue && m.Home)
                    RecEv.Add(new AEv { K = 'H', T = t, C1 = FacHex(prev), C2 = owner == int.MinValue ? null : FacHex(owner), N = 1, PX = m.P.x, PY = m.P.y, PZ = m.P.z, HasPos = true });
                else if (owner != int.MinValue)
                    RecEv.Add(new AEv { K = 'C', T = t, C1 = FacHex(owner), C2 = prev == int.MinValue ? null : FacHex(prev), N = 1, PX = m.P.x, PY = m.P.y, PZ = m.P.z, HasPos = true });
                else if (prev != int.MinValue)
                    RecEv.Add(new AEv { K = 'N', T = t, C1 = FacHex(prev), N = 1, PX = m.P.x, PY = m.P.y, PZ = m.P.z, HasPos = true });
            }
            for (int i = 0; i < NestGone.Count; i++) NestMems.Remove(NestGone[i]);
        }

        // ------------------------------------------------------------------ snapshots (playback)
        private static int FacIndex(int id)
        {
            int i = SnapFacIds.IndexOf(id);
            if (i < 0 && SnapFacIds.Count < 120) { SnapFacIds.Add(id); i = SnapFacIds.Count - 1; }
            return i < 0 ? 119 : i;
        }

        private static void TakeSnapshot()
        {
            float total = BtcReplay.PlayTotal;
            if (Mathf.Abs(total - _snapTotal) > 0.5f) { Snaps.Clear(); SnapFacIds.Clear(); _snapFrontier = 0f; _snapTotal = total; }
            float pc = BtcReplay.PlayClock;
            int slot = Mathf.RoundToInt(pc / SnapEvery);
            Snap old;
            if (Snaps.TryGetValue(slot, out old) && Mathf.Abs(pc - slot * SnapEvery) >= Mathf.Abs(old.T - slot * SnapEvery) - 0.15f) return;
            Snap s = new Snap { T = pc };
            List<short> ux = new List<short>(), uz = new List<short>(); List<byte> uf = new List<byte>();
            float tm = Time.time;
            for (int i = 0; i < Labors.Count; i++)
            {
                Labor lab = Labors[i];
                if (lab == null || !lab.gameObject.activeInHierarchy) continue;
                if (!BtcMatchStats.Tracks(lab.faction)) continue;
                BricktronDamageReceiver dr = lab.GetComponent<BricktronDamageReceiver>();
                if (dr != null && dr.CurrentHP <= 0f) continue;
                Vector3 p = lab.transform.position;
                float lh;
                bool eng = LastHit.TryGetValue(lab.gameObject.GetInstanceID(), out lh) && tm - lh <= EngagedWindow;
                ux.Add((short)Mathf.Clamp(Mathf.RoundToInt(p.x), -32000, 32000));
                uz.Add((short)Mathf.Clamp(Mathf.RoundToInt(p.z), -32000, 32000));
                uf.Add((byte)(FacIndex(lab.faction.ID) | (eng ? 0x80 : 0)));
            }
            s.Ux = ux.ToArray(); s.Uz = uz.ToArray(); s.Uf = uf.ToArray();
            List<short> cx = new List<short>(), cz = new List<short>(); List<byte> co = new List<byte>();
            for (int i = 0; i < Nests.Count; i++)
            {
                FireflyNest n = Nests[i];
                if (n == null) continue;
                AffiliationComponent aff = n.GetComponent<AffiliationComponent>();
                Vector3 p = n.transform.position;
                cx.Add((short)Mathf.Clamp(Mathf.RoundToInt(p.x), -32000, 32000));
                cz.Add((short)Mathf.Clamp(Mathf.RoundToInt(p.z), -32000, 32000));
                co.Add(aff != null && BtcMatchStats.Tracks(aff.faction) ? (byte)FacIndex(aff.faction.ID) : (byte)255);
            }
            s.Cx = cx.ToArray(); s.Cz = cz.ToArray(); s.Co = co.ToArray();
            Snaps[slot] = s;
            if (pc > _snapFrontier) _snapFrontier = pc;
        }

        private static Snap FindSnap(float t)
        {
            int slot = Mathf.RoundToInt(t / SnapEvery);
            Snap s;
            for (int d = 0; d <= 1; d++)
            {
                if (Snaps.TryGetValue(slot - d, out s)) return s;
                if (d > 0 && Snaps.TryGetValue(slot + d, out s)) return s;
            }
            return null;
        }

        // ------------------------------------------------------------------ mini map highlights (playback)
        private sealed class FightMark
        {
            internal GameObject Go;
            internal MiniMap.Trackable Tr;
            internal float Seen;
            internal bool Large, Used;
        }

        private static readonly Dictionary<int, FightMark> _marks = new Dictionary<int, FightMark>();
        private static MiniMap _mm;
        private static Sprite _fightSprite;
        private static readonly List<int> CellKeys = new List<int>();
        private static readonly List<float> CellX = new List<float>(), CellZ = new List<float>();
        private static readonly List<int> CellN = new List<int>();
        private static readonly List<int> Gone = new List<int>();

        private static readonly List<float> CellY = new List<float>();
        private static readonly List<int> CellFA = new List<int>(), CellFB = new List<int>();
        private static readonly List<float> EY = new List<float>();

        /// <summary>Proximity clusters of the units that are in combat (centroid sums in CellX/Y/Z, count, up to two factions).</summary>
        private static void BuildClusters()
        {
            CellKeys.Clear(); CellX.Clear(); CellY.Clear(); CellZ.Clear(); CellN.Clear(); CellFA.Clear(); CellFB.Clear();
            for (int i = 0; i < EX.Count; i++)
            {
                int best = -1; float bd = ClusterR * ClusterR;
                for (int j = 0; j < CellKeys.Count; j++)
                {
                    float cx = CellX[j] / CellN[j] - EX[i], cz = CellZ[j] / CellN[j] - EZ[i];
                    float d = cx * cx + cz * cz;
                    if (d < bd) { bd = d; best = j; }
                }
                if (best < 0)
                {
                    CellKeys.Add(0); CellX.Add(EX[i]); CellY.Add(EY[i]); CellZ.Add(EZ[i]); CellN.Add(1); CellFA.Add(EF[i]); CellFB.Add(int.MinValue);
                }
                else
                {
                    CellX[best] += EX[i]; CellY[best] += EY[i]; CellZ[best] += EZ[i]; CellN[best]++;
                    if (CellFA[best] != EF[i] && CellFB[best] == int.MinValue) CellFB[best] = EF[i];
                }
            }
        }

        private static int _markId;
        private static float _nextDiag;
        private const float ClusterR = 26f;

        private static void UpdateMinimap(bool large)
        {
            if (!BtcConfig.ReplayMinimapFights.Value) { if (_marks.Count > 0) ClearMarks(); return; }
            if (_mm == null) _mm = UnityEngine.Object.FindObjectOfType<MiniMap>();
            float now = Time.unscaledTime;
            if (now >= _nextDiag)
            {
                _nextDiag = now + 5f;
                BtcLog.Info("AutoMarks", "minimap: engaged=" + EX.Count + " clusters=" + CellKeys.Count + " marks=" + _marks.Count + " mm=" + (_mm != null) + " hits=" + LastHit.Count + " large=" + large);
            }
            if (_mm == null) return;
            Gone.Clear();
            foreach (KeyValuePair<int, FightMark> kv in _marks) kv.Value.Used = false;
            int shown = 0;
            for (int i = 0; i < CellKeys.Count && shown < 16; i++)
            {
                if (CellN[i] < 2) continue;
                shown++;
                Vector3 pos = new Vector3(CellX[i] / CellN[i], 0f, CellZ[i] / CellN[i]);
                FightMark m = null; float bd = 40f * 40f;
                foreach (KeyValuePair<int, FightMark> kv in _marks)
                {
                    if (kv.Value.Used || kv.Value.Go == null) continue;
                    Vector3 q = kv.Value.Go.transform.position - pos;
                    float d = q.x * q.x + q.z * q.z;
                    if (d < bd) { bd = d; m = kv.Value; }
                }
                if (m == null) { m = new FightMark(); _marks[++_markId] = m; }
                m.Used = true;
                if (m.Go == null) { m.Go = new GameObject("BtcFightMark"); m.Go.hideFlags = HideFlags.HideAndDontSave; }
                m.Go.transform.position = pos;
                m.Seen = now;
                m.Large = large;
                if (m.Tr == null || m.Tr.Transform == null)
                {
                    if (_fightSprite == null) _fightSprite = BtcMarkIcons.FightSprite();
                    m.Tr = MiniMap.AddTrackable(m.Go.transform, _fightSprite, new Color(1f, 0.55f, 0.1f, 1f));
                    BtcLog.Info("AutoMarks", "minimap mark added at " + pos.x.ToString("F0") + "," + pos.z.ToString("F0") + " units=" + CellN[i]);
                }
            }
            foreach (KeyValuePair<int, FightMark> kv in _marks)
                if (now - kv.Value.Seen > 1.6f) Gone.Add(kv.Key);
            for (int i = 0; i < Gone.Count; i++) RemoveMark(Gone[i]);
        }

        private static void RemoveMark(int key)
        {
            FightMark m;
            if (!_marks.TryGetValue(key, out m)) return;
            try { if (m.Tr != null && m.Tr.Transform != null) MiniMap.RemoveTrackable(m.Tr); } catch { }
            if (m.Go != null) UnityEngine.Object.Destroy(m.Go);
            _marks.Remove(key);
        }

        private static void ClearMarks()
        {
            List<int> keys = new List<int>(_marks.Keys);
            for (int i = 0; i < keys.Count; i++) RemoveMark(keys[i]);
        }

        private static void PulseMarks()
        {
            if (_marks.Count == 0) return;
            float s = Mathf.Sin(Time.unscaledTime * 6f);
            foreach (KeyValuePair<int, FightMark> kv in _marks)
            {
                FightMark m = kv.Value;
                if (m.Tr == null || m.Tr.TargetImage == null) continue;
                Color c = m.Large ? new Color(1f, 0.15f, 0.1f, 1f) : new Color(1f, 0.6f, 0.1f, 1f);
                c.a = 0.65f + 0.35f * s;
                m.Tr.TargetImage.color = c;
                float sc = (m.Large ? 1.5f : 1.1f) * (1f + 0.15f * s);
                m.Tr.TargetImage.rectTransform.localScale = new Vector3(sc, sc, 1f);
            }
        }

        // ------------------------------------------------------------------ timeline lane (playback UI)
        private static List<Item> _items = new List<Item>();
        private static int _layoutKey = -1;
        private static int _rows;
        private static GUIStyle _badge, _tip;
        private static string _tipText;
        private static float _tipX, _tipY;
        private static Color _tipCol;
        private static float _lane, _chipRow;

        private static void LoadHidden()
        {
            if (_hiddenLoaded) return;
            _hiddenLoaded = true;
            string s = BtcConfig.ReplayHiddenKinds != null ? BtcConfig.ReplayHiddenKinds.Value : "";
            for (int i = 0; i < s.Length; i++) HiddenKinds.Add(s[i]);
        }

        private static void SaveHidden()
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in HiddenKinds) sb.Append(c);
            if (BtcConfig.ReplayHiddenKinds != null) BtcConfig.ReplayHiddenKinds.Value = sb.ToString();
        }

        private static bool HasKind(char k)
        {
            for (int i = 0; i < Play.Count; i++) if (Play[i].K == k) return true;
            if (k == 'Z')
                for (int i = 0; i < BtcTimer.Play.Count; i++) if (BtcTimer.Play[i].K == 'Z') return true;
            return false;
        }

        private static void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, BtcUiStyle.White);
            GUI.color = old;
        }

        private static void DrawIcon(char k, Rect r, string c1, string c2)
        {
            BtcMarkIcons.Draw(k, r, ParseCol(c1), ParseCol((k == 'H' || k == 'N' ? null : c2) ?? c1));
        }

        private static void Layout(float width, float total, float iw)
        {
            int key = (int)width * 31 + Play.Count * 7 + BtcTimer.Play.Count * 3 + (int)(total * 0.1f);
            foreach (char c in HiddenKinds) key = key * 13 + c;
            if (key == _layoutKey) return;
            _layoutKey = key;
            _items.Clear();
            float gap = iw * 0.95f;
            // pass 1: merge neighbours of the same kind and colours by pixel gap
            Dictionary<string, Item> last = new Dictionary<string, Item>();
            for (int pass = 0; pass < 2; pass++)
            {
                int n = pass == 0 ? Play.Count : BtcTimer.Play.Count;
                for (int i = 0; i < n; i++)
                {
                    char k; float t, a; int cnt; string c1, c2; float px = 0f, py = 0f, pz = 0f; bool hp = false;
                    if (pass == 0) { AEv e = Play[i]; k = e.K; t = e.T; a = e.A; cnt = e.N; c1 = e.C1; c2 = e.C2; px = e.PX; py = e.PY; pz = e.PZ; hp = e.HasPos; }
                    else
                    {
                        BtcTimer.TlEv z = BtcTimer.Play[i];
                        if (z.K != 'Z') continue;
                        k = 'Z'; t = z.T; a = z.A; cnt = 1; c1 = "FFC857"; c2 = null;
                    }
                    if (HiddenKinds.Contains(k)) continue;
                    float x = width * Mathf.Clamp01(t / Mathf.Max(0.001f, total));
                    string gk = k + "|" + c1 + "|" + c2;
                    Item it;
                    if (last.TryGetValue(gk, out it) && x - it.X < gap)
                    {
                        it.N += cnt;
                        it.A = Mathf.Max(it.A, a);
                        continue;
                    }
                    it = new Item { K = k, X = x, T = t, A = a, N = cnt, C1 = c1, C2 = c2, PX = px, PY = py, PZ = pz, HasPos = hp };
                    last[gk] = it;
                    _items.Add(it);
                }
            }
            _items.Sort(delegate (Item p, Item q) { return p.X.CompareTo(q.X); });
            // pass 2: rows (no overlap between different groups)
            float[] rowEnd = new float[3] { -999f, -999f, -999f };
            _rows = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                int r = 0;
                while (r < 2 && it.X - rowEnd[r] < gap) r++;
                it.Row = r;
                rowEnd[r] = it.X;
                if (r + 1 > _rows) _rows = r + 1;
            }
        }

        private static string KindName(char k)
        {
            switch (k)
            {
                case 'D': return BtcGameplay.T("Bricktron deaths", "Смерти бриктронов");
                case 'C': return BtcGameplay.T("Crystal", "Кристалл");
                case 'N': return BtcGameplay.T("Crystal neutralised", "Кристалл стал нейтральным");
                case 'H': return BtcGameplay.T("Home crystal lost (defeat)", "Главный кристалл потерян (поражение)");
                case 'X': return BtcGameplay.T("Barrel explosion", "Взрыв бочки");
                case 'F': return BtcGameplay.T("Large fight", "Масштабный файт");
                case 'Z': return BtcGameplay.T("Pause", "Пауза");
            }
            return "";
        }

        private static string Clock(float s)
        {
            int m = Mathf.FloorToInt(s / 60f);
            return m + ":" + Mathf.FloorToInt(s % 60f).ToString("00");
        }

        private static readonly char[] Kinds = { 'D', 'C', 'N', 'H', 'X', 'F', 'Z' };

        /// <summary>Draws the lane + kind chips above the panel; returns the vertical space used (to lift the colour chips and the tooltip).</summary>
        internal static float DrawLane(Rect bar, float h, float total, Rect panel)
        {
            LoadHidden();
            _lane = 0f; _chipRow = 0f;
            bool any = false;
            for (int i = 0; i < Kinds.Length && !any; i++) any = HasKind(Kinds[i]);
            List<string> pcols = BtcTimer.MarkColors();
            if (pcols.Count > 0) any = true;
            if (!any) return 0f;
            Event e = Event.current;
            float iw = h * 0.78f;
            Layout(bar.width, total, iw);
            float rowH = iw + 4f;
            int rows = Mathf.Max(1, _rows);
            _lane = rows * rowH + 6f;
            float cs = h * 0.62f;
            _chipRow = cs + 6f;
            float povH = 6f;
            Rect strip = new Rect(bar.x, panel.y - povH - _lane, bar.width, _lane);
            if (_items.Count > 0) Box(strip, new Color(0f, 0f, 0f, 0.42f));

            // fight spans on the bar
            if (!HiddenKinds.Contains('F'))
                for (int i = 0; i < Play.Count; i++)
                {
                    AEv f = Play[i];
                    if (f.K != 'F') continue;
                    float x0 = bar.x + bar.width * Mathf.Clamp01(f.T / total);
                    float x1 = bar.x + bar.width * Mathf.Clamp01(f.A / total);
                    Box(new Rect(x0, bar.yMax - 3f, Mathf.Max(3f, x1 - x0), 3f), new Color(1f, 0.25f, 0.15f, 0.9f));
                }

            Item hover = null;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                float cx = bar.x + it.X;
                float y = strip.yMax - 3f - (it.Row + 1) * rowH + 2f;
                Rect ir = new Rect(cx - iw * 0.5f, y, iw, iw);
                // thin stem to the bar
                Box(new Rect(cx - 0.5f, ir.yMax, 1f, Mathf.Max(0f, strip.yMax - ir.yMax)), new Color(1f, 1f, 1f, 0.28f));
                DrawIcon(it.K, ir, it.C1, it.C2);
                if (it.N > 1 && it.K != 'Z')
                {
                    if (_badge == null) { _badge = new GUIStyle(GUI.skin.label); _badge.alignment = TextAnchor.MiddleCenter; _badge.fontStyle = FontStyle.Bold; _badge.normal.textColor = Color.white; _badge.clipping = TextClipping.Overflow; }
                    _badge.fontSize = Mathf.Max(9, (int)(h * 0.42f));
                    string t = "x" + it.N;
                    float bw = _badge.fontSize * (0.62f * t.Length + 0.5f);
                    Rect br = new Rect(ir.xMax - bw * 0.55f, ir.yMax - _badge.fontSize * 0.95f, bw, _badge.fontSize * 1.0f);
                    Box(br, new Color(0f, 0f, 0f, 0.82f));
                    GUI.Label(br, t, _badge);
                }
                if (ir.Contains(e.mousePosition)) hover = it;
                if (e.type == EventType.MouseDown && e.button == 0 && ir.Contains(e.mousePosition))
                {
                    GoToEvent(it);
                    e.Use();
                }
            }

            // kind chips (left, above the strip)
            float cy = strip.y - cs - 4f;
            float cxp = panel.x + 4f;
            for (int i = 0; i < Kinds.Length; i++)
            {
                char k = Kinds[i];
                if (!HasKind(k)) continue;
                Rect cr = new Rect(cxp, cy, cs, cs);
                bool hidden = HiddenKinds.Contains(k);
                Box(new Rect(cr.x - 1f, cr.y - 1f, cr.width + 2f, cr.height + 2f), new Color(0f, 0f, 0f, 0.8f));
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, hidden ? 0.28f : 1f);
                BtcMarkIcons.Draw(k, cr, hidden ? new Color(0.6f, 0.6f, 0.6f) : ChipCol(k), hidden ? new Color(0.6f, 0.6f, 0.6f) : ChipCol(k));
                GUI.color = old;
                if (cr.Contains(e.mousePosition))
                {
                    _tipText = KindName(k) + (hidden ? "  (" + BtcGameplay.T("hidden", "скрыто") + ")" : "");
                    _tipX = cr.center.x; _tipY = cr.y - 4f; _tipCol = Color.white;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (hidden) HiddenKinds.Remove(k); else HiddenKinds.Add(k);
                        SaveHidden();
                        _layoutKey = -1;
                        e.Use();
                    }
                    hover = null;
                    DrawTip(h);
                }
                cxp += cs + 5f;
            }

            // player mark colours in the same row
            if (pcols.Count > 0 && cxp > panel.x + 5f) cxp += cs * 0.4f;
            for (int i = 0; i < pcols.Count; i++)
            {
                Rect cr = new Rect(cxp, cy, cs, cs);
                bool hidden = BtcTimer.HiddenCols.Contains(pcols[i]);
                Box(new Rect(cr.x - 1f, cr.y - 1f, cr.width + 2f, cr.height + 2f), new Color(0f, 0f, 0f, 0.8f));
                Color pc = ParseCol(pcols[i]);
                if (hidden) pc.a = 0.25f;
                Box(cr, pc);
                Box(new Rect(cr.x + cr.width * 0.3f, cr.y + cr.height * 0.18f, cr.width * 0.4f, cr.height * 0.64f), new Color(0f, 0f, 0f, hidden ? 0.15f : 0.45f));
                if (cr.Contains(e.mousePosition))
                {
                    _tipText = BtcGameplay.T("Player marks", "Метки игроков") + (hidden ? "  (" + BtcGameplay.T("hidden", "скрыто") + ")" : "");
                    _tipX = cr.center.x; _tipY = cr.y - 4f; _tipCol = ParseCol(pcols[i]);
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (hidden) BtcTimer.HiddenCols.Remove(pcols[i]); else BtcTimer.HiddenCols.Add(pcols[i]);
                        e.Use();
                    }
                    hover = null;
                    DrawTip(h);
                }
                cxp += cs + 5f;
            }

            if (hover != null)
            {
                string name = KindName(hover.K);
                if (hover.K == 'D') name += " x" + hover.N;
                else if (hover.K == 'F') name += " (" + hover.N + ", " + Mathf.RoundToInt(Mathf.Max(0f, hover.A - hover.T)) + " " + BtcGameplay.T("s", "с") + ")";
                else if (hover.K == 'C') name = hover.C2 != null ? BtcGameplay.T("Crystal stolen", "Кристалл отбит") : BtcGameplay.T("Crystal captured", "Кристалл захвачен");
                else if (hover.K == 'Z') name += " " + Mathf.RoundToInt(Mathf.Max(0f, hover.A - hover.T)) + " " + BtcGameplay.T("s", "с");
                _tipText = name + "  " + Clock(hover.T);
                _tipX = bar.x + hover.X;
                _tipY = strip.yMax - 3f - (hover.Row + 1) * rowH - 2f;
                _tipCol = ParseCol(hover.C1);
                DrawTip(h);
            }
            return povH + _lane + _chipRow + 4f;
        }

        private static Color ChipCol(char k)
        {
            switch (k)
            {
                case 'D': return new Color(0.9f, 0.9f, 0.92f);
                case 'C': return new Color(0.45f, 0.85f, 1f);
                case 'N': return new Color(0.6f, 0.75f, 0.85f);
                case 'H': return new Color(1f, 0.45f, 0.55f);
                case 'X': return new Color(1f, 0.6f, 0.15f);
                case 'F': return new Color(1f, 0.35f, 0.3f);
                default: return new Color(1f, 0.78f, 0.34f);
            }
        }

        // ------------------------------------------------------------------ click: seek + camera
        private static bool _camPending, _camHas;
        private static Vector3 _camPos;
        private static float _camSeekT, _camUntil, _camNext;
        private static int _camTimes;

        private static void LookAt(Vector3 p)
        {
            CameraScript cam = CameraScript.Instance;
            if (cam != null) cam.LookAt(p);
        }

        private static void GoToEvent(Item it)
        {
            _camSeekT = Mathf.Max(0f, it.T - 5f);
            BtcReplay.SeekTo(_camSeekT);
            _camHas = it.HasPos;
            if (!it.HasPos) { _camPending = false; return; }
            _camPos = new Vector3(it.PX, it.PY, it.PZ);
            LookAt(_camPos);
            _camPending = true; _camTimes = 0; _camNext = 0f;
            _camUntil = Time.unscaledTime + 120f;
        }

        /// <summary>After a seek (a rewind reloads the scene and resets the camera) put the camera on the event place again, a few times.</summary>
        private static void CameraTick()
        {
            if (!_camPending) return;
            float now = Time.unscaledTime;
            if (now > _camUntil || _camTimes >= 4) { _camPending = false; return; }
            if (BtcReplay.Seeking || now < _camNext) return;
            float pc = BtcReplay.PlayClock;
            if (pc < _camSeekT - 0.5f || pc > _camSeekT + 20f) return;
            _camNext = now + 0.5f;
            _camTimes++;
            LookAt(_camPos);
        }

        private static void DrawTip(float h)
        {
            if (_tip == null) { _tip = new GUIStyle(GUI.skin.label); _tip.alignment = TextAnchor.MiddleLeft; _tip.normal.textColor = Color.white; }
            _tip.fontSize = Mathf.Max(11, (int)(h * 0.5f));
            float w = _tip.fontSize * (0.56f * _tipText.Length + 2.2f);
            float hh = _tip.fontSize * 1.7f;
            Rect r = new Rect(Mathf.Clamp(_tipX - w * 0.5f, 2f, Screen.width - w - 2f), _tipY - hh, w, hh);
            Box(r, new Color(0f, 0f, 0f, 0.92f));
            Box(new Rect(r.x + 3f, r.y + 4f, 4f, r.height - 8f), _tipCol);
            GUI.Label(new Rect(r.x + 12f, r.y, r.width - 12f, r.height), _tipText, _tip);
        }

        // ------------------------------------------------------------------ Alt preview
        private static Bounds _bounds;
        private static bool _boundsOk;
        private static Texture2D _hm;

        /// <summary>Alt + hover on the timeline: mini map of the replay at time t, drawn above (bottomY) around centre x.</summary>
        internal static void DrawPreview(float t, float cx, float bottomY, float h)
        {
            float size = Mathf.Min(Screen.height * 0.34f, 360f);
            Rect r = new Rect(Mathf.Clamp(cx - size * 0.5f, 4f, Screen.width - size - 4f), bottomY - size - h * 0.9f, size, size + h * 0.9f);
            Box(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(0f, 0f, 0f, 0.92f));
            Rect map = new Rect(r.x, r.y + h * 0.9f, size, size);
            Box(map, new Color(0.08f, 0.1f, 0.12f, 1f));
            if (_tip == null) { _tip = new GUIStyle(GUI.skin.label); _tip.normal.textColor = Color.white; }
            _tip.fontSize = Mathf.Max(11, (int)(h * 0.5f));
            _tip.alignment = TextAnchor.MiddleCenter;
            Snap s = FindSnap(t);
            GUI.Label(new Rect(r.x, r.y, r.width, h * 0.9f), Clock(t) + (s == null ? "" : ""), _tip);
            if (_mm == null) _mm = UnityEngine.Object.FindObjectOfType<MiniMap>();
            if (_mm != null && _mm.heightmap != null)
            {
                _bounds = _mm.mapBounds; _hm = _mm.heightmap; _boundsOk = true;
            }
            if (_boundsOk && _hm != null)
            {
                Rect uv = new Rect(_bounds.min.x / 1024f, _bounds.min.z / 1024f, _bounds.size.x / 1024f, _bounds.size.z / 1024f);
                GUI.DrawTextureWithTexCoords(map, _hm, uv);
            }
            if (s == null)
            {
                _tip.alignment = TextAnchor.MiddleCenter;
                GUI.Label(map, BtcGameplay.T("Loading preview...", "Загрузка превью...") + "\n" + BtcGameplay.T("watched up to ", "просмотрено до ") + Clock(_snapFrontier), _tip);
                return;
            }
            if (!_boundsOk)
            {
                // no mini map object: fit the snapshot
                float x0 = 99999f, x1 = -99999f, z0 = 99999f, z1 = -99999f;
                for (int i = 0; i < s.Ux.Length; i++) { x0 = Mathf.Min(x0, s.Ux[i]); x1 = Mathf.Max(x1, s.Ux[i]); z0 = Mathf.Min(z0, s.Uz[i]); z1 = Mathf.Max(z1, s.Uz[i]); }
                for (int i = 0; i < s.Cx.Length; i++) { x0 = Mathf.Min(x0, s.Cx[i]); x1 = Mathf.Max(x1, s.Cx[i]); z0 = Mathf.Min(z0, s.Cz[i]); z1 = Mathf.Max(z1, s.Cz[i]); }
                float sz = Mathf.Max(40f, Mathf.Max(x1 - x0, z1 - z0) + 30f);
                _bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f), new Vector3(sz, 0f, sz));
            }
            float bx = _bounds.min.x, bz = _bounds.min.z, bs = Mathf.Max(1f, _bounds.size.x);
            for (int i = 0; i < s.Cx.Length; i++)
            {
                Vector2 p = ToMap(map, s.Cx[i], s.Cz[i], bx, bz, bs);
                Color col = s.Co[i] == 255 ? new Color(0.8f, 0.8f, 0.8f) : FacColor(s.Co[i]);
                float d = Mathf.Max(8f, h * 0.5f);
                Box(new Rect(p.x - d * 0.5f - 1.5f, p.y - d * 0.5f - 1.5f, d + 3f, d + 3f), Color.black);
                Box(new Rect(p.x - d * 0.5f, p.y - d * 0.5f, d, d), col);
            }
            for (int i = 0; i < s.Ux.Length; i++)
            {
                Vector2 p = ToMap(map, s.Ux[i], s.Uz[i], bx, bz, bs);
                bool eng = (s.Uf[i] & 0x80) != 0;
                int fi = s.Uf[i] & 0x7F;
                if (eng) Box(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), new Color(1f, 0.15f, 0.1f, 0.95f));
                Box(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), Color.black);
                Box(new Rect(p.x - 1.8f, p.y - 1.8f, 3.6f, 3.6f), FacColor(fi));
            }
        }

        private static Vector2 ToMap(Rect map, float x, float z, float bx, float bz, float bs)
        {
            float u = Mathf.Clamp01((x - bx) / bs), v = Mathf.Clamp01((z - bz) / bs);
            return new Vector2(map.x + u * map.width, map.yMax - v * map.height);
        }

        private static Color FacColor(int idx)
        {
            if (idx < 0 || idx >= SnapFacIds.Count) return Color.white;
            Faction f = BtcMatchStats.FactionOf(SnapFacIds[idx]);
            return f != null ? f.FactionColor : Color.white;
        }

        internal static bool AltDown { get { return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt); } }
    }

    /// <summary>Procedural icons (one style: white fill that is tinted per player, dark outline), no asset files.</summary>
    internal static class BtcMarkIcons
    {
        private const int S = 40;
        private sealed class Set { internal Texture2D Out, L, R, Comp; }
        private static readonly Dictionary<char, Set> Sets = new Dictionary<char, Set>();

        private static readonly float[] Arrow = { 9f, 35f, 9f, 9f, 14.5f, 14f, 18.5f, 5f, 22.5f, 7f, 18.5f, 15.5f, 26f, 15.5f };
        private static readonly float[] Flag = { 12f, 35f, 22f, 33.5f, 33f, 31f, 28.5f, 26f, 33f, 21f, 22f, 23.5f, 12f, 21f };

        /// <summary>Signed distance to a polygon (even-odd inside test, nearest edge distance).</summary>
        private static float PolySd(float x, float y, float[] p)
        {
            int n = p.Length / 2;
            bool inside = false;
            float best = 1e9f;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) inside = !inside;
                best = Mathf.Min(best, Seg(x, y, xi, yi, xj, yj));
            }
            return inside ? -best : best;
        }

        private static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float qx = ax + dx * t - px, qy = ay + dy * t - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        /// <summary>Signed distance (pixels, negative inside) of icon k; y is up.</summary>
        private static float Sd(char k, float x, float y)
        {
            float dx = x - 20f, dy = y - 20f;
            switch (k)
            {
                case 'D':
                    {
                        float head = Mathf.Sqrt(dx * dx + (y - 23f) * (y - 23f)) - 12.5f;
                        float jaw = Mathf.Max(Mathf.Abs(dx) - 7f, Mathf.Abs(y - 10.5f) - 5.5f);
                        float d = Mathf.Min(head, jaw);
                        float e1 = Mathf.Sqrt((x - 14.5f) * (x - 14.5f) + (y - 22f) * (y - 22f)) - 3.6f;
                        float e2 = Mathf.Sqrt((x - 25.5f) * (x - 25.5f) + (y - 22f) * (y - 22f)) - 3.6f;
                        float nose = Mathf.Max(Mathf.Abs(dx) - 1.4f, Mathf.Abs(y - 14.5f) - 1.8f);
                        return Mathf.Max(d, -Mathf.Min(Mathf.Min(e1, e2), nose));
                    }
                case 'C':
                    return (Mathf.Abs(dx) / 11f + Mathf.Abs(dy) / 16f - 1f) * 9f;
                case 'N': // crystal crossed out: the diamond is cut by a gap and a slash runs through it
                    {
                        float dia = (Mathf.Abs(dx) / 11f + Mathf.Abs(dy) / 16f - 1f) * 9f;
                        float sl = Seg(x, y, 6f, 34f, 34f, 6f);
                        return Mathf.Min(Mathf.Max(dia, -(sl - 4.6f)), sl - 1.9f);
                    }
                case 'H': // flag: pole and a waving cloth
                    {
                        float pole = Mathf.Max(Mathf.Abs(x - 11.5f) - 1.6f, Mathf.Abs(y - 19f) - 17f);
                        float cloth = PolySd(x, y, Flag);
                        return Mathf.Min(pole, cloth);
                    }
                case 'X':
                    {
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Atan2(dy, dx);
                        float spike = 10f + 9.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 2.2f);
                        return Mathf.Max(r - spike, -(r - 4.5f)) ;
                    }
                case 'F':
                    {
                        float a = Seg(x, y, 8f, 8f, 32f, 32f) - 2.6f;
                        float b = Seg(x, y, 32f, 8f, 8f, 32f) - 2.6f;
                        return Mathf.Min(a, b);
                    }
                case 'A': // cursor arrow (polygon)
                    return PolySd(x, y, Arrow);
                case 'W': // single sword: blade, cross guard, grip, pommel
                    {
                        float blade = Mathf.Max(Mathf.Abs(dx) - 3f - Mathf.Max(0f, (y - 26f)) * 0.5f, Mathf.Max(6f - y, y - 36f));
                        float guard = Mathf.Max(Mathf.Abs(dx) - 9.5f, Mathf.Abs(y - 11f) - 2.4f);
                        float grip = Mathf.Max(Mathf.Abs(dx) - 2f, Mathf.Abs(y - 6f) - 4f);
                        float pom = Mathf.Sqrt(dx * dx + (y - 2.5f) * (y - 2.5f)) - 3.2f;
                        return Mathf.Min(Mathf.Min(blade, guard), Mathf.Min(grip, pom));
                    }
                default: // 'Z' pause: two bars in a rounded disc
                    {
                        float disc = Mathf.Sqrt(dx * dx + dy * dy) - 17f;
                        float b1 = Mathf.Max(Mathf.Abs(x - 15f) - 3f, Mathf.Abs(dy) - 8f);
                        float b2 = Mathf.Max(Mathf.Abs(x - 25f) - 3f, Mathf.Abs(dy) - 8f);
                        return Mathf.Max(disc, -Mathf.Min(b1, b2));
                    }
            }
        }

        private static Set Get(char k)
        {
            Set s;
            if (Sets.TryGetValue(k, out s) && s.Out != null) return s;
            s = new Set();
            Color[] o = new Color[S * S], l = new Color[S * S], r = new Color[S * S], c = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = 0f;
                    // 2x2 supersample of the distance field
                    for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++) d += Sd(k, x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy);
                    d *= 0.25f;
                    float fill = Mathf.Clamp01(0.5f - d);
                    float outl = Mathf.Clamp01(2.6f - d);
                    int i = y * S + x;
                    o[i] = new Color(0f, 0f, 0f, outl);
                    l[i] = new Color(1f, 1f, 1f, x < S / 2 ? fill : 0f);
                    r[i] = new Color(1f, 1f, 1f, x >= S / 2 ? fill : 0f);
                    float a = Mathf.Max(fill, outl);
                    float w = a > 0f ? fill / a : 0f;
                    c[i] = new Color(w, w, w, a);
                }
            s.Out = Tex(o); s.L = Tex(l); s.R = Tex(r); s.Comp = Tex(c);
            Sets[k] = s;
            return s;
        }

        private static Texture2D Tex(Color[] px)
        {
            Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        /// <summary>Icon k in r: left half tinted a, right half tinted b (same colour = plain icon).</summary>
        internal static void Draw(char k, Rect r, Color a, Color b)
        {
            Set s = Get(k);
            Color old = GUI.color;
            float al = old.a;
            GUI.color = new Color(1f, 1f, 1f, al);
            GUI.DrawTexture(r, s.Out);
            Rect lr = new Rect(r.x, r.y, r.width * 0.5f, r.height);
            Rect rr = new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height);
            GUI.color = new Color(a.r, a.g, a.b, al);
            GUI.DrawTextureWithTexCoords(lr, s.L, new Rect(0f, 0f, 0.5f, 1f));
            GUI.color = new Color(b.r, b.g, b.b, al);
            GUI.DrawTextureWithTexCoords(rr, s.R, new Rect(0.5f, 0f, 0.5f, 1f));
            GUI.color = old;
        }

        private static Texture2D _ring, _disc;

        /// <summary>Soft white ring (or translucent disc with a ring edge) for the pie menu and click ripples.</summary>
        internal static Texture2D RingTex(bool filled)
        {
            Texture2D t = filled ? _disc : _ring;
            if (t != null) return t;
            const int N = 64;
            Color[] px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f));
                    float edge = Mathf.Clamp01(3.2f - Mathf.Abs(d - 29f));
                    float fill = filled ? Mathf.Clamp01(30f - d) * 0.28f : 0f;
                    px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Max(edge, fill));
                }
            t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            if (filled) _disc = t; else _ring = t;
            return t;
        }

        /// <summary>Composite icon k as a sprite (white fill tinted by the Image colour, dark outline), pivot in 0..1.</summary>
        internal static Sprite SpriteOf(char k, Vector2 pivot)
        {
            return Sprite.Create(Get(k).Comp, new Rect(0f, 0f, S, S), pivot, 100f);
        }

        internal static Sprite FightSprite()
        {
            Texture2D t = Get('F').Comp;
            return Sprite.Create(t, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
