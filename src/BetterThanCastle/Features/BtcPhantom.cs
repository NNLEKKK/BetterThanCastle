using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Bricktron;
using Brix.Game.Components;
using Brix.Game.Semantique;
using Brix.Lifecycle.Pooling;
using Brix.Network;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Phantoms (Gameplay Settings > Phantoms, group "phantom"). Every player gets N extra workers spawned by the host at the home crystal.
    /// A phantom is a plain Builder bricktron that: is not counted anywhere (bricktron cap, spawn cost progression, refund: kept out of
    /// Faction._unitCount), has 1 HP and 0 stamina whatever it wears, deals 0 damage to units with its fists (an armed phantom hits normally),
    /// cannot damage enemy objects or blocks in enemy territory, never counts for crystal capture, ignores call to arms, sees vis.phantom x
    /// the unit vision, leaves no corpse and no respawn firefly (whatever it carried vanishes), and comes back after a cooldown.
    /// Host-authoritative: the host spawns, kills and enforces; clients only mark the units (look, vision, count) from msg 118.
    /// BetterConnection ON only (rules come from BtcGameplay.E, vanilla values when BC is OFF; no handler, no send while OFF).
    /// Saves and keyframe seeks do not keep the phantom mark (a loaded phantom is a normal worker).
    /// </summary>
    internal static class BtcPhantom
    {
        internal const short MsgState = 118;

        private sealed class Mark
        {
            internal GameObject Go;
            internal uint NetId;
            internal bool Dead;
            internal int BaseVis = -1;
        }

        private sealed class Slot
        {
            internal GameObject Go;
            internal float RespawnAt;
        }

        private static readonly Dictionary<int, Mark> Marks = new Dictionary<int, Mark>();
        private static readonly Dictionary<int, List<Slot>> Slots = new Dictionary<int, List<Slot>>();   // faction ID -> slots (host)
        private static readonly Dictionary<int, FireflyNest> Homes = new Dictionary<int, FireflyNest>();  // faction ID -> home crystal (host)
        private static readonly List<Vector3> NestPos = new List<Vector3>();
        private static readonly List<Faction> NestFac = new List<Faction>();
        private static readonly List<int> Tmp = new List<int>();
        private static readonly List<Mark> Back = new List<Mark>();
        private static readonly HashSet<uint> HostIds = new HashSet<uint>();
        private static readonly Dictionary<int, Faction> CountedIn = new Dictionary<int, Faction>();   // unit instance id -> faction whose vanilla count includes it (mirror, while the rule is on)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Live phantoms on this peer (fast exit for every hook).</summary>
        internal static int LiveCount;
        /// <summary>Host: true while BtcPhantom spawns a unit (its faction change is not counted).</summary>
        internal static bool Spawning;
        /// <summary>The rule is on (set every tick): the unit-count hooks mirror vanilla counting.</summary>
        internal static bool Track;

        private static bool _wasOn, _dirty;
        private static float _nextStep, _nextNest, _nextSend, _nextClient, _nextLog;
        private static float _clock;   // game seconds since the match started (when the advanced timer is not running)
        private static uint[] _lastIds = new uint[0];
        private static int _spawned, _died;

        private static int iOn = -1, iCount, iCd, iPen, iPeace, iDelay, iVis = -1;

        // ------------------------------------------------------------------ rules
        internal static void AddRows()
        {
            const string G = "phantom";
            BtcGameplay.Add("phantom.on", G, "Phantoms", "Фантомы", 0f, 0f, 1f, 1f, true,
                "Every player gets extra ghost workers at the home crystal. A phantom works, carries and crafts like a worker, but is not counted in the bricktron limit or the spawn price, has 1 HP, deals no damage to units with its fists, cannot damage enemy buildings, never takes or holds crystals, ignores call to arms and leaves no corpse: it vanishes with what it carried and comes back after a cooldown. Critical rule change. Needs BetterConnection ON for everybody.",
                "Каждый игрок получает дополнительных рабочих-призраков у домашнего кристалла. Фантом работает, носит и крафтит как рабочий, но не считается в лимите бриктронов и в цене спавна, имеет 1 ХП, не наносит урона юнитам кулаками, не может ломать вражеские постройки, никогда не захватывает и не удерживает кристаллы, игнорирует призыв к оружию и не оставляет трупа: исчезает вместе с тем, что нёс, и возвращается после перезарядки. Критичное изменение правил. Нужен BetterConnection ВКЛ у всех.");
            BtcGameplay.Add("phantom.count", G, "Phantoms per player", "Фантомов у каждого игрока", 2f, 1f, 20f, 1f, false,
                "How many phantoms every player has.", "Сколько фантомов у каждого игрока.");
            BtcGameplay.Add("phantom.cd", G, "Respawn cooldown (s)", "Перезарядка возрождения (с)", 15f, 1f, 300f, 1f, false,
                "Seconds a dead phantom waits before it appears again at the home crystal.", "Сколько секунд погибший фантом ждёт, прежде чем снова появиться у домашнего кристалла.");
            BtcGameplay.Add("phantom.pen", G, "Energy lost per phantom death", "Штраф энергии за смерть фантома", 0f, 0f, 1000f, 5f, false,
                "Energy taken from the owner's home crystal every time a phantom dies (never below 0). 0 = no penalty.", "Сколько энергии забирается из домашнего кристалла владельца при каждой смерти фантома (не ниже 0). 0 = без штрафа.");
            BtcGameplay.Add("phantom.peace", G, "Appear when peace time ends", "Появляются после мирного времени", 0f, 0f, 1f, 1f, true,
                "On: phantoms appear when the peace time of the advanced timer ends (at once if there is no peace time). Off: after the delay below.", "Вкл: фантомы появляются, когда кончается мирное время продвинутого таймера (сразу, если мирного времени нет). Выкл: после задержки ниже.");
            BtcGameplay.Add("phantom.delay", G, "Appear after (s, 0 = at once)", "Появляются через (с, 0 = сразу)", 0f, 0f, 3600f, 10f, false,
                "Game time of the advanced timer after which the phantoms appear. 0 = right at the start.", "Игровое время продвинутого таймера, после которого появляются фантомы. 0 = сразу на старте.");
            BtcGameplay.Add("vis.phantom", "vis", "Vision: phantoms (x of units)", "Обзор: фантомы (x от юнитов)", 0.25f, 0f, 5f, 0.05f, false,
                "Vision radius of phantoms as a share of the normal unit vision (Vision: units above). 0.25 = a quarter.", "Радиус обзора фантомов как доля обычного обзора юнитов (Обзор: юниты выше). 0.25 = четверть.");
            iOn = BtcGameplay.Idx("phantom.on"); iCount = BtcGameplay.Idx("phantom.count"); iCd = BtcGameplay.Idx("phantom.cd"); iPen = BtcGameplay.Idx("phantom.pen");
            iPeace = BtcGameplay.Idx("phantom.peace"); iDelay = BtcGameplay.Idx("phantom.delay"); iVis = BtcGameplay.Idx("vis.phantom");
        }

        private static float Val(int i, float def)
        {
            float[] e = BtcGameplay.E;
            return (e == null || i < 0 || i >= e.Length) ? def : e[i];
        }

        internal static bool RuleOn { get { return iOn >= 0 && BtcNetworkGate.Enabled && Val(iOn, 0f) > 0.5f; } }
        internal static int Count { get { return Mathf.Clamp(Mathf.RoundToInt(Val(iCount, 2f)), 0, 50); } }
        internal static float Cooldown { get { return Mathf.Max(0.5f, Val(iCd, 15f)); } }
        internal static int Penalty { get { return Mathf.Max(0, Mathf.RoundToInt(Val(iPen, 0f))); } }
        internal static float VisionFactor { get { return Mathf.Max(0f, Val(iVis, 0.25f)); } }

        private static bool Host { get { return NetworkServer.active && BtcReplay.Mode != BtcReplay.State.Playing; } }

        private static bool InMatch()
        {
            if (BtcGameplay.InLobby) return false;
            return GameSignals.Received<Action>(GameSignals.LevelReady);
        }

        // ------------------------------------------------------------------ queries (hot paths: no allocations)
        /// <summary>The object is a live phantom on this peer.</summary>
        internal static bool IsPhantom(GameObject go)
        {
            if (LiveCount == 0 || go == null) return false;
            Mark m;
            return Marks.TryGetValue(go.GetInstanceID(), out m) && !m.Dead;
        }

#if DEV
        internal static List<GameObject> DevLive()
        {
            List<GameObject> l = new List<GameObject>();
            foreach (Mark m in Marks.Values) if (!m.Dead && m.Go != null) l.Add(m.Go);
            return l;
        }
#endif

        private static bool Known(GameObject go)
        {
            return go != null && Marks.Count > 0 && Marks.ContainsKey(go.GetInstanceID());
        }

        // ------------------------------------------------------------------ unit count (Faction._unitCount never includes a phantom)
        private static readonly FieldInfo FUnitCount = AccessTools.Field(typeof(Faction), "_unitCount");

        private static void AdjustCount(Faction f, int d)
        {
            if (f == null || FUnitCount == null) return;
            FUnitCount.SetValue(f, Mathf.Max(0, (int)FUnitCount.GetValue(f) + d));
        }

        /// <summary>Faction.AnyUnitChangedFaction prefix (called once per faction): false = skip the vanilla count change.
        /// While the rule is on, every count change vanilla makes is mirrored in CountedIn, so a unit marked later (clients) is subtracted only if it was counted.</summary>
        internal static bool OnFactionChange(Faction self, GameObject unit, Faction oldF, Faction newF)
        {
            if (unit == null) return true;
            int id = unit.GetInstanceID();
            Mark m;
            if (Marks.TryGetValue(id, out m))
            {
                if (!m.Dead) return false;
                Marks.Remove(id);          // a pooled dead phantom comes back as something else: count it normally
            }
            else if (Spawning && unit.GetComponent<Labor>() != null)
            {
                Marks[id] = new Mark { Go = unit };
                LiveCount++;
                return false;
            }
            if (unit.GetComponent<Labor>() != null)
            {
                Faction c;
                if (newF == self) CountedIn[id] = self;
                else if (oldF == self && CountedIn.TryGetValue(id, out c) && c == self) CountedIn.Remove(id);
            }
            return true;
        }

        /// <summary>Faction.RemoveUnit prefix (Labor deleted): false = skip the vanilla decrement. The mark stays (dead) because every faction gets the call.</summary>
        internal static bool OnLaborDeleted(Labor labor)
        {
            if (labor == null) return true;
            if (!Known(labor.gameObject)) { if (CountedIn.Count > 0) CountedIn.Remove(labor.gameObject.GetInstanceID()); return true; }
            Mark m = Marks[labor.gameObject.GetInstanceID()];
            if (!m.Dead) { m.Dead = true; LiveCount--; }
            return false;
        }

        /// <summary>Marks an existing unit as a phantom on this peer; takes it out of the faction count if vanilla counted it.</summary>
        private static void Adopt(GameObject go, uint netId)
        {
            int id = go.GetInstanceID();
            Mark m;
            if (Marks.TryGetValue(id, out m))
            {
                if (!m.Dead) { m.NetId = netId; return; }
                Marks.Remove(id);
            }
            m = new Mark { Go = go, NetId = netId };
            Marks[id] = m;
            LiveCount++;
            Faction c;
            if (CountedIn.TryGetValue(id, out c)) { AdjustCount(c, -1); CountedIn.Remove(id); }
            ApplyVision(m);
            BtcPhantomLook.Ensure(go);
        }

        /// <summary>Client: the host no longer lists this unit as a phantom (but it lives on): back to a normal unit.</summary>
        private static void Unadopt(Mark m)
        {
            RestoreVision(m);
            BtcPhantomLook.Remove(m.Go);
            Labor l = m.Go.GetComponent<Labor>();
            if (l != null && l.faction != null) { AdjustCount(l.faction, 1); CountedIn[m.Go.GetInstanceID()] = l.faction; }
            Marks.Remove(m.Go.GetInstanceID());
            LiveCount--;
        }

        // ------------------------------------------------------------------ vision
        private static void ApplyVision(Mark m)
        {
            LocalFactionVision v = m.Go.GetComponent<LocalFactionVision>();
            if (v == null) return;
            if (m.BaseVis < 0) m.BaseVis = v.LineOfSightRange;
            v.LineOfSightRange = Mathf.Max(0, Mathf.RoundToInt(m.BaseVis * VisionFactor));
        }

        private static void RestoreVision(Mark m)
        {
            if (m.BaseVis < 0 || m.Go == null) return;
            LocalFactionVision v = m.Go.GetComponent<LocalFactionVision>();
            if (v != null) v.LineOfSightRange = m.BaseVis;
            m.BaseVis = -1;
        }

        // ------------------------------------------------------------------ combat rules (host hooks)
        private static bool Allied(Faction a, Faction b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return a.AllyFactions != null && a.AllyFactions.Contains(b);
        }

        /// <summary>True = the phantom attacker must not damage this receiver (fists vs units, enemy objects, blocks in enemy territory).</summary>
        internal static bool VetoAttack(object recv, GameObject attacker, Vector3 contact)
        {
            if (!IsPhantom(attacker)) return false;
            if (recv is BricktronDamageReceiver)
            {
                Occupation o = attacker.GetComponent<Occupation>();
                return o == null || o.CurrentOccupation == Occupation.Type.Builder || o.CurrentOccupation == Occupation.Type.Unassigned;
            }
            if (recv is TerrainDamageReceiver || recv is NatureDamageReceiver) return false;
            Faction af = Faction.GetFaction(attacker);
            Component c = recv as Component;
            if (c != null)
            {
                Faction tf = Faction.GetFaction(c.gameObject);
                if (tf != null) return !Allied(af, tf);
            }
            return EnemyTerritory(af, ShieldHookUtil.PosOf(recv, contact));
        }

        /// <summary>Blocks have no owner: the nearest player crystal (home or captured) within 50 blocks decides whose they are.</summary>
        private static bool EnemyTerritory(Faction mine, Vector3 p)
        {
            float best = 50f * 50f;
            Faction owner = null;
            for (int i = 0; i < NestPos.Count; i++)
            {
                float d = (NestPos[i] - p).sqrMagnitude;
                if (d < best) { best = d; owner = NestFac[i]; }
            }
            return owner != null && !Allied(mine, owner);
        }

        /// <summary>CaptureClock.SetUnitsInCaptureZone: phantoms neither capture nor defend.</summary>
        internal static void FilterCapture(HashSet<GameObject> set)
        {
            bool any = false;
            foreach (GameObject go in set) if (IsPhantom(go)) { any = true; break; }
            if (any) set.RemoveWhere(IsPhantom);
        }

        /// <summary>Host: HP 1 and stamina 0 whatever the phantom wears (Profession.SetHpBonus/SetStaminaBonus postfix and the 4 Hz step).</summary>
        internal static void Enforce(GameObject go)
        {
            if (!NetworkServer.active || !IsPhantom(go)) return;
            BricktronDamageReceiver dr = go.GetComponent<BricktronDamageReceiver>();
            if (dr == null) return;
            if (dr.MaxHP != 1f) dr.MaxHP = 1f;
            if (dr.CurrentHP > 1f) dr.CurrentHP = 1f;
            if (dr.MaxStamina != 0f) dr.MaxStamina = 0f;
            if (dr.CurrentStamina > 0f) dr.CurrentStamina = 0f;
        }

        // ------------------------------------------------------------------ death (Locomotion4.RpcDie prefix, every peer)
        private static readonly AccessTools.FieldRef<Locomotion4, bool> FCorpse = AccessTools.FieldRefAccess<Locomotion4, bool>("IsCorpse");
        private static MethodInfo _consume;

        /// <summary>True = handled (no corpse, no firefly, carried things vanish, the unit goes back to the pool).</summary>
        internal static bool OnDie(Locomotion4 loco)
        {
            GameObject go = loco.gameObject;
            Mark m;
            if (!Marks.TryGetValue(go.GetInstanceID(), out m) || m.Dead) return false;
            if (FCorpse(loco)) return false;
            FCorpse(loco) = true;
            bool server = NetworkServer.active;
            if (Host) HostDied(go);
            RestoreVision(m);
            BtcPhantomLook.Remove(go);
            if (server)
            {
                Recepteur[] rs = go.GetComponentsInChildren<Recepteur>(true);
                for (int i = 0; i < rs.Length; i++) { try { rs[i].Wipe(); } catch (Exception e) { BtcLog.Warn("Phantom", "wipe: " + e.Message); } }
            }
            Transferable[] ts = go.GetComponentsInChildren<Transferable>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                GameObject t = ts[i].gameObject;
                if (t == go) continue;
                t.transform.SetParent(null, true);
                bool net = t.GetComponent<NetworkIdentity>() != null;
                if (server) t.SafeNetworkDestroy();
                else if (!net) ObjectPoolSingleton.Release(t);
            }
            m.Dead = true;
            LiveCount--;
            _died++;
            GameObject flail = loco.FlailingCollider;
            if (flail != null && go.transform.IsChildOf(flail.transform)) { ObjectPoolSingleton.Release(go); ObjectPoolSingleton.Release(flail); }
            else { if (flail != null) ObjectPoolSingleton.Release(flail); ObjectPoolSingleton.Release(go); }
            return true;
        }

        private static void HostDied(GameObject go)
        {
            float now = Time.time;
            foreach (KeyValuePair<int, List<Slot>> kv in Slots)
            {
                List<Slot> l = kv.Value;
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i].Go != go) continue;
                    l[i].Go = null;
                    l[i].RespawnAt = now + Cooldown;
                    int pen = Penalty;
                    FireflyNest home;
                    if (pen > 0 && Homes.TryGetValue(kv.Key, out home) && home != null)
                    {
                        try
                        {
                            if (_consume == null) _consume = AccessTools.Method(typeof(FireflyNest), "ConsumeUnnamedFirefliesUpTo");
                            int got = _consume != null ? (int)_consume.Invoke(home, new object[] { pen }) : 0;
                            BtcLog.Info("Phantom", "death penalty faction=" + kv.Key + " -" + Mathf.Min(got, pen));
                        }
                        catch (Exception e) { BtcLog.Warn("Phantom", "penalty: " + e.Message); }
                    }
                    _dirty = true;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ call to arms (UNetProjectCmd.CmdAddCallToArmsGoal prefix)
        internal static bool SkipCallToArms(GameObject laborGO) { return IsPhantom(laborGO); }

        // ------------------------------------------------------------------ tick (ResourcePackManager.Update via BtcTimer.Tick)
        internal static void Tick()
        {
            try
            {
                Track = RuleOn;
                bool on = Track && InMatch();
                if (!on)
                {
                    if (_wasOn) Clear("off");
                    return;
                }
                if (!_wasOn) { _wasOn = true; _clock = 0f; BtcLog.Info("Phantom", "on (host=" + Host + ", count=" + Count + ")"); }
                _clock += Time.deltaTime;
                BtcPhantomLook.CheckPack();
                float un = Time.unscaledTime;
                if (Host)
                {
                    float now = Time.time;
                    if (now >= _nextStep) { _nextStep = now + 0.25f; HostStep(now); }
                    if (_dirty && un >= _nextSend - 1.5f || un >= _nextSend) { _dirty = false; _nextSend = un + 2f; Broadcast(); }
                }
                else if (un >= _nextClient) { _nextClient = un + 0.5f; ClientStep(); }
                if (un >= _nextLog) { _nextLog = un + 30f; BtcLog.Info("Phantom", "live=" + LiveCount + " marks=" + Marks.Count + " spawned=" + _spawned + " died=" + _died); }
            }
            catch (Exception e) { BtcLog.Error("Phantom", "tick: " + e); }
        }

        private static void Clear(string why)
        {
            _wasOn = false;
            foreach (Mark m in Marks.Values)
            {
                if (m.Dead || m.Go == null) continue;
                RestoreVision(m);
                BtcPhantomLook.Remove(m.Go);
            }
            Marks.Clear();
            Slots.Clear();
            Homes.Clear();
            NestPos.Clear(); NestFac.Clear();
            HostIds.Clear();
            CountedIn.Clear();
            _lastIds = new uint[0];
            LiveCount = 0;
            _nextStep = _nextNest = _nextSend = 0f;
            BtcLog.Info("Phantom", "cleared (" + why + ") spawned=" + _spawned + " died=" + _died);
            _spawned = _died = 0;
        }

        /// <summary>Phantoms may appear: peace over (rule on) or the delay passed. Waiting/3-2-1 of the advanced timer = not yet.</summary>
        private static bool Started()
        {
            if (BtcTimer.Cur == BtcTimer.Phase.Waiting || BtcTimer.Cur == BtcTimer.Phase.Counting) return false;
            bool timer = BtcTimer.Cur == BtcTimer.Phase.Running;
            float el = timer ? BtcTimer.Elapsed : _clock;
            if (Val(iPeace, 0f) > 0.5f) return !timer || el >= BtcTimer.PeaceSec;
            return el >= Val(iDelay, 0f);
        }

        private static bool IsPlayer(Faction f) { return f != null && BtcMatchStats.Tracks(f) && !f.isAI; }

        private static void RefreshNests()
        {
            Homes.Clear();
            NestPos.Clear(); NestFac.Clear();
            List<FireflyNest> list = BtcMatchStats.NestList();
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                FireflyNest n = list[i];
                if (n == null) continue;
                Faction f = n.faction;
                if (!IsPlayer(f)) continue;
                NestPos.Add(n.transform.position); NestFac.Add(f);
                if (n.isHome && !Homes.ContainsKey(f.ID)) Homes[f.ID] = n;
            }
        }

        private static void HostStep(float now)
        {
            if (now >= _nextNest || Homes.Count == 0) { _nextNest = now + 1f; RefreshNests(); }
            bool started = Started();
            int n = Count, budget = 4;
            foreach (KeyValuePair<int, FireflyNest> kv in Homes)
            {
                FireflyNest home = kv.Value;
                if (home == null) continue;
                List<Slot> l;
                if (!Slots.TryGetValue(kv.Key, out l)) { l = new List<Slot>(); Slots[kv.Key] = l; }
                while (l.Count < n) l.Add(new Slot());
                for (int i = 0; i < l.Count; i++)
                {
                    Slot s = l[i];
                    if (s.Go != null)
                    {
                        if (IsPhantom(s.Go) && s.Go.activeInHierarchy) { Enforce(s.Go); continue; }
                        s.Go = null;                                       // gone without RpcDie (fell out, released)
                        if (s.RespawnAt < now) s.RespawnAt = now + Cooldown;
                        _dirty = true;
                    }
                    if (i >= n || !started || now < s.RespawnAt || budget <= 0) continue;
                    budget--;
                    s.Go = Spawn(home);
                    if (s.Go == null) s.RespawnAt = now + 2f;
                }
            }
        }

        private static GameObject Spawn(FireflyNest home)
        {
            Faction f = home.faction;
            Vector3 p = home.transform.position + Vector3.up * 4f + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));
            GameObject go;
            Spawning = true;
            try { go = BtcDuel.SpawnUnit(BtcDuelUnit.Worker, f, p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)); }
            finally { Spawning = false; }
            if (go == null) { BtcLog.Warn("Phantom", "spawn failed for faction " + f.ID); return null; }
            NetworkIdentity ni = go.GetComponent<NetworkIdentity>();
            uint id = ni != null ? ni.netId.Value : 0u;
            Mark m;
            if (!Marks.TryGetValue(go.GetInstanceID(), out m) || m.Dead)
            {
                BtcLog.Warn("Phantom", "spawn: no faction change seen, adopting");
                Adopt(go, id);
                Marks.TryGetValue(go.GetInstanceID(), out m);
            }
            else
            {
                m.NetId = id;
                ApplyVision(m);
                BtcPhantomLook.Ensure(go);
            }
            Nom nom = go.GetComponent<Nom>();
            if (nom != null) nom.SetNom("Phantom");
            Enforce(go);
            BricktronDamageReceiver dr = go.GetComponent<BricktronDamageReceiver>();
            if (dr != null) dr.CurrentStamina = 0f;
            _spawned++;
            _dirty = true;
            return go;
        }

        // ------------------------------------------------------------------ wire: msg 118 host -> clients "1|netId,netId,..."
        private static void Broadcast()
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                StringBuilder sb = new StringBuilder("1|");
                bool first = true;
                foreach (Mark m in Marks.Values)
                {
                    if (m.Dead || m.NetId == 0u) continue;
                    if (!first) sb.Append(',');
                    sb.Append(m.NetId.ToString(Inv));
                    first = false;
                }
                StringMessage msg = new StringMessage(sb.ToString());
                for (int i = 0; i < NetworkServer.connections.Count; i++)
                {
                    NetworkConnection c = NetworkServer.connections[i];
                    if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) c.Send(MsgState, msg);
                }
            }
            catch (Exception e) { BtcLog.Error("Phantom", "Broadcast: " + e); }
        }

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
            catch (Exception e) { BtcLog.Error("Phantom", "RegisterHandlers: " + e); }
        }

        private static void OnState(NetworkMessage nm)
        {
            try
            {
                StringMessage msg = nm.ReadMessage<StringMessage>();
                if (Host || !BtcNetworkGate.Enabled || msg == null || msg.value == null) return;
                string[] parts = msg.value.Split('|');
                if (parts.Length < 2 || parts[0] != "1") return;
                string[] ids = parts[1].Length == 0 ? new string[0] : parts[1].Split(',');
                uint[] a = new uint[ids.Length];
                for (int i = 0; i < ids.Length; i++) uint.TryParse(ids[i], NumberStyles.Integer, Inv, out a[i]);
                _lastIds = a;
                ClientStep();
            }
            catch (Exception e) { BtcLog.Error("Phantom", "OnState: " + e); }
        }

        /// <summary>Client: adopt every listed unit that exists; units the host no longer lists go back to normal.</summary>
        private static void ClientStep()
        {
            if (!RuleOn || !InMatch()) return;
            HostIds.Clear();
            for (int i = 0; i < _lastIds.Length; i++)
            {
                uint id = _lastIds[i];
                if (id == 0u) continue;
                HostIds.Add(id);
                GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
                if (go == null || !go.activeInHierarchy) continue;
                if (!IsPhantom(go)) Adopt(go, id);
                else BtcPhantomLook.Ensure(go);
            }
            Tmp.Clear();
            foreach (KeyValuePair<int, Mark> kv in Marks)
            {
                Mark m = kv.Value;
                if (m.Dead) { if (m.Go == null) Tmp.Add(kv.Key); continue; }
                if (m.Go == null) { Tmp.Add(kv.Key); LiveCount--; continue; }
                if (!HostIds.Contains(m.NetId))
                {
                    if (!m.Go.activeInHierarchy) { m.Dead = true; LiveCount--; }
                    else Back.Add(m);   // live but no longer a phantom
                }
            }
            for (int i = 0; i < Tmp.Count; i++) Marks.Remove(Tmp[i]);
            for (int i = 0; i < Back.Count; i++) Unadopt(Back[i]);
            Back.Clear();
        }
    }
}
