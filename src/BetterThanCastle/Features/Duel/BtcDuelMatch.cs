using System;
using System.Collections.Generic;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Input;
using Brix.Lua;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
    internal enum BtcDuelState { Idle = 0, Loading, Countdown, Fight, Over }

    /// <summary>
    /// Duel match driver (one MonoBehaviour, created by BtcDuel.Launch). Owns the state machine, the rules (safe zone, kill and escape,
    /// space control), infinite heal, the bot tick and the HUD. Rematch/new settings never reload the map: units are despawned and respawned.
    /// Hot path: Update runs the rules at 10 Hz and the bot at its own rate; no per-frame allocations.
    /// </summary>
    internal sealed class BtcDuelMatch : MonoBehaviour
    {
        internal const float CountdownSec = 3f;
        internal const float SpaceControlSec = 10f;
        internal const float RulesStep = 0.1f;

        internal BtcDuelState State = BtcDuelState.Idle;
        internal readonly List<BtcDuelUnitRef> Units = new List<BtcDuelUnitRef>();
        internal readonly Faction[] Fac = new Faction[2];
        internal readonly bool[] Escaping = new bool[2];
        internal readonly float[] Ctrl = new float[2];
        internal int Winner = -1;
        internal string Reason = "";
        internal float CountdownLeft;
        internal float FightTime;
        internal int MatchNo;

        private float _readySince = -1f, _builtAt = -1f, _calmSince = -1f;
        private float _rulesAcc, _healAcc, _snapAcc;
        public const float HealStep = 1f, HealPerStep = 10f;
        private int _nextId;
        private readonly int[] _hits = new int[2], _skips = new int[2];
        private int[] _kills { get { return Kills; } }
        private readonly StringBuilder _sb = new StringBuilder(512);
        private IDuelBot _bot, _botP;
        internal readonly float[] AvgDmg = { 32f, 32f };
        private Vector2 _scroll;

        /// <summary>The match runs on the duel's empty map: 1-block slab over the void instead of the plateau.</summary>
        internal bool VoidMap;
        private float _loadAt, _sweepAt;
        /// <summary>Round results so far (host counts, clients mirror it: a new number = a new result to record).</summary>
        internal int ResultSeq;
        /// <summary>Result of the last finished round (a give-up starts the next round at once, so Winner is already reset when the message goes out).</summary>
        internal int LastWinner = -1, LastK0, LastK1;
        internal string LastReason = "";
        internal float LastTime;
        private int _seqSeen = -1;
        internal readonly int[] Kills = new int[2];
        // client mirror: arena geometry from the host
        private int _gCx, _gCz, _gScale = 1, _bCx = int.MinValue, _bCz, _bScale;
        private bool _clientLooked;

        internal void BeginLoad()
        {
            State = BtcDuelState.Loading;
            _readySince = -1f; _builtAt = -1f; _loadAt = Time.realtimeSinceStartup;
            Units.Clear(); BtcDuel.ById.Clear();
            MatchNo = 0; ResultSeq = 0; _seqSeen = -1;
        }

        internal void BeginClient()
        {
            State = BtcDuelState.Loading;
            Units.Clear(); BtcDuel.ById.Clear();
            _bCx = int.MinValue; _clientLooked = false; _seqSeen = -1;
            _bot = null; _botP = null;
        }

        // ================= update =================
        private void Update()
        {
            if (!BtcDuel.Active) return;
            try
            {
                if (BtcDuel.IsClient) { ClientTick(); return; }
                switch (State)
                {
                    case BtcDuelState.Loading: TickLoading(); break;
                    case BtcDuelState.Countdown:
                        CountdownLeft -= Time.deltaTime;
                        if (CountdownLeft <= 0f) StartFight();
                        break;
                    case BtcDuelState.Fight: TickFight(Time.deltaTime); break;
                }
                if (State == BtcDuelState.Loading && BtcDuel.Pvp && !NetworkServer.active && Time.realtimeSinceStartup - _loadAt > 30f) Deactivate("duel host session gone");
                if (State != BtcDuelState.Loading && State != BtcDuelState.Idle)
                {
                    if (!NetworkServer.active) { Deactivate("session ended"); return; }
                    if (Time.unscaledTime > _sweepAt) { _sweepAt = Time.unscaledTime + 3f; SweepStrays(); CheckOpponent(); }
                    if (BtcDuel.Pvp) { BtcDuelNet.HostTick(); if (_gone && State != BtcDuelState.Over) OpponentGone(); }
                }
            }
            catch (Exception e) { BtcLog.Error("Duel", "Update", e); }
        }

        private void TickLoading()
        {
            if (!Ready())
            {
                _readySince = -1f;
                // the empty map gave no faction: remember it, the next start uses the default map (same guard as the aim trainer)
                if (VoidMap && !BtcDuel.Pvp && Time.realtimeSinceStartup - _loadAt > 25f && Architecte.commence && Architecte.physicsReady && (User.LocalUser == null || User.LocalUser.faction == null))
                {
                    BtcDuelConfig.EmptyMap = false;
                    BtcLog.Warn("Duel", "empty map: no local faction after 25 s, switching to the default map");
                    BtcHud.Notice(BtcGameplay.T("The empty map does not work here: press Start again (the default map will be used)", "Пустая карта тут не работает: нажми Старт ещё раз (будет обычная карта)"), BtcReplayIcons.Swords, BtcHud.Warn);
                    ExitToMenu();
                }
                if (BtcDuel.Pvp && Time.realtimeSinceStartup - _loadAt > 180f) { BtcHud.Notice(BtcGameplay.T("The opponent did not load", "Соперник не загрузился"), BtcReplayIcons.Cross, BtcHud.Warn); Deactivate("pvp load timeout"); }
                return;
            }
            if (_readySince < 0f) _readySince = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _readySince < (VoidMap ? 0.5f : 2f)) return;
            if (_builtAt >= 0f) { SettleStep(); return; }
            if (!FindFactions()) { Deactivate("no second faction"); return; }
            BtcDuelMap.SetScale(BtcDuelConfig.ArenaScale);
            bool ok;
            if (VoidMap) { Vector3 c = WorldCentre(); ok = BtcDuelMap.BuildVoid(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.z)); }
            else ok = BtcDuelMap.Build(Anchor());
            if (!ok) { BtcLog.Error("Duel", "arena build failed, mode aborted"); Deactivate("arena build failed"); return; }
            BtcLog.Info("Duel", "arena " + BtcDuelMap.Describe());
            ClearForeignUnits();
            LookAtArena();
            _builtAt = Time.realtimeSinceStartup; _calmSince = -1f;
        }

        /// <summary>No home crystal near the arena on the empty map: the middle of the camera bounds (same as the aim trainer).</summary>
        private static Vector3 WorldCentre()
        {
            // the camera bounds are not always set when the arena is built (seen: centre 0,0 = arena half outside the voxel grid)
            try
            {
                CameraScript cs = FindObjectOfType<CameraScript>();
                if (cs != null)
                {
                    Bounds b = cs.BoundsLimit;
                    if (b.min.x >= 0f && b.min.z >= 0f && b.size.x > BtcDuelMap.Len + 40f && b.size.z > BtcDuelMap.Wid + 40f) return b.center;
                    BtcLog.Info("Duel", "camera bounds unusable (" + b.min + ".." + b.max + "): world centre");
                }
            }
            catch (Exception) { }
            return new Vector3(512f, 0f, 512f);   // the stock Void heightmap is 1024 x 1024
        }

        /// <summary>Terrain/navigation rebuild after the arena edit: wait until the frame rate recovers before the countdown
        /// (plateau: 3 s calm, max 90 s; the thin void slab: 0.5 s calm, max 5 s).</summary>
        private void SettleStep()
        {
            float now = Time.realtimeSinceStartup;
            bool calm = Time.unscaledDeltaTime < 0.03f;
            if (!calm) _calmSince = -1f; else if (_calmSince < 0f) _calmSince = now;
            float need = BtcDuelMap.OnVoid ? 0.5f : 3f, max = BtcDuelMap.OnVoid ? 5f : 90f;
            if ((_calmSince >= 0f && now - _calmSince > need) || now - _builtAt > max)
            {
                BtcLog.Info("Duel", "arena settled after " + (now - _builtAt).ToString("0.0") + " s");
                _builtAt = -1f;
                StartCountdown();
            }
        }

        private bool Ready()
        {
            try
            {
                if (!Architecte.commence || !Architecte.physicsReady || !VoxelEngine.Ready) return false;
                User u = User.LocalUser;
                if (u == null || u.faction == null) return false;
                if (BtcDuel.Pvp && OpponentFaction() == null) return false;
                return VoidMap || Anchor() != Vector3.zero;
            }
            catch (Exception) { return false; }
        }

        /// <summary>PvP: the faction of the other playing user (not a spectator, not the replay recorder).</summary>
        private static Faction OpponentFaction()
        {
            foreach (User u in BtcRegistry.UsersArray())
            {
                if (u == null || u == User.LocalUser || u.faction == null || u.faction.IsSpectator || u.Network_screenName == "REC") continue;
                try { if (BtcReplay.IsRec(u.connectionToClient)) continue; } catch (Exception) { }
                return u.faction;
            }
            return null;
        }

        private bool _gone;

        /// <summary>PvP host: the opponent's user disappeared (left / dropped).</summary>
        internal void CheckOpponent()
        {
            if (!BtcDuel.Pvp || BtcDuel.IsClient || Fac[1] == null) return;
            bool found = false;
            foreach (User u in BtcRegistry.UsersArray()) if (u != null && u.faction == Fac[1]) { found = true; break; }
            _gone = !found;
        }

        private void OpponentGone()
        {
            BtcLog.Info("Duel", "opponent left");
            if (State == BtcDuelState.Fight) Finish(0, "opponent left");
            else { State = BtcDuelState.Over; Winner = -1; Reason = "opponent left"; }
            BtcHud.Notice(BtcGameplay.T(BtcDuel.GuestNick + " left the duel", BtcDuel.GuestNick + " покинул дуэль"), BtcReplayIcons.Cross, BtcHud.Warn);
        }

        private static Vector3 Anchor()
        {
            try
            {
                User u = User.LocalUser;
                if (u == null || u.faction == null) return Vector3.zero;
                LuaStaticDriver sd;
                if (LuaDriverHQ.AllStatics.TryGetValue("fy_Crystal", out sd))
                {
                    LuaDriver d = sd.GetFirstOfFaction(u.faction);
                    if (d != null) return d.transform.position;
                }
                Labor[] ls = FindObjectsOfType<Labor>();
                for (int i = 0; i < ls.Length; i++)
                    if (ls[i] != null && u.faction.IsSame(ls[i].gameObject)) return ls[i].transform.position;
            }
            catch (Exception) { }
            return Vector3.zero;
        }

        private bool FindFactions()
        {
            Fac[0] = User.LocalUser.faction;
            Fac[1] = null;
            List<Faction> all = Faction.GetAllFactions();
            if (BtcDuel.Pvp) Fac[1] = OpponentFaction();
            else
                for (int i = 0; i < all.Count; i++)
                {
                    Faction f = all[i];
                    if (f == null || f == Fac[0] || f.HasOwner() || f.IsSpectator || f.isAI || f == Faction.DefaultNeutral) continue;
                    Fac[1] = f; break;
                }
            if (Fac[1] == null) { BtcLog.Error("Duel", "no faction for side 1 (pvp=" + BtcDuel.Pvp + " factions=" + all.Count + ")"); return false; }
            bool enemy = false;
            try
            {
                Fac[0].SetFactionRelation(Fac[1], Affiliation.Relation.Enemy);
                Fac[1].SetFactionRelation(Fac[0], Affiliation.Relation.Enemy);
                enemy = Fac[0].IsEnemyTo(Fac[1].gameObject);
            }
            catch (Exception e) { BtcLog.Warn("Duel", "relation: " + e.Message); }
            BtcLog.Info("Duel", "factions player=" + Fac[0].name + " bot=" + Fac[1].name + " enemies=" + enemy);
            return true;
        }

        /// <summary>Removes the starting workers of both factions (they stand under the plateau).</summary>
        private void ClearForeignUnits()
        {
            int n = 0;
            Labor[] ls = FindObjectsOfType<Labor>();
            for (int i = 0; i < ls.Length; i++)
            {
                Labor l = ls[i];
                if (l == null) continue;
                GameObject go = l.gameObject;
                if (Fac[0].IsSame(go) || Fac[1].IsSame(go)) { BtcDuel.Despawn(go); n++; }
            }
            BtcLog.Info("Duel", "removed start units: " + n);
        }

        /// <summary>Every 3 s: units of both sides that are not duel units (workers the home crystal hands out) are removed.</summary>
        private void SweepStrays()
        {
            if (Fac[0] == null || Fac[1] == null) return;
            Labor[] ls = FindObjectsOfType<Labor>();
            int n = 0;
            for (int i = 0; i < ls.Length; i++)
            {
                Labor l = ls[i];
                if (l == null || BtcDuel.ById.ContainsKey(l.gameObject.GetInstanceID())) continue;
                if (Fac[0].IsSame(l.gameObject) || Fac[1].IsSame(l.gameObject)) { BtcDuel.Despawn(l.gameObject); n++; }
            }
            if (n > 0) BtcLog.Info("Duel", "stray units removed: " + n);
        }

        private static void LookAtArena()
        {
            try
            {
                CameraScript cam = FindObjectOfType<CameraScript>();
                if (cam != null) cam.LookAt(new Vector3(BtcDuelMap.CenterX, BtcDuelMap.Floor, BtcDuelMap.CenterZ));
            }
            catch (Exception e) { BtcLog.Warn("Duel", "camera: " + e.Message); }
        }

        // ================= match flow =================
        internal void StartCountdown()
        {
            DespawnAll();
            MatchNo++;
            Winner = -1; Reason = "";
            Escaping[0] = Escaping[1] = false; Ctrl[0] = Ctrl[1] = 0f;
            _hits[0] = _hits[1] = _skips[0] = _skips[1] = _kills[0] = _kills[1] = 0;
            FightTime = 0f; _rulesAcc = _healAcc = _snapAcc = 0f;
            BtcDuel.SkipAnims = BtcDuelConfig.SkipAnims;
            BtcDuel.WantNext[0] = BtcDuel.WantNext[1] = false;
            State = BtcDuelState.Countdown;
            CountdownLeft = CountdownSec;
            if (BtcDuel.Pvp) { BtcDuel.SetupLine = BtcDuelPvp.SettingsLine(); BtcDuelNet.SendNow(); }
            BtcDuelLog.Begin();
            BtcDuelLog.Line("CFG", "match=" + MatchNo + " diff=" + BtcDuelConfig.Difficulty + " skip=" + BtcDuelConfig.SkipAnims + " heal=" + BtcDuelConfig.InfiniteHeal +
                " player=" + Fmt(BtcDuelConfig.PlayerCount) + " bot=" + Fmt(BtcDuelConfig.BotCount) + " arenaX0=" + BtcDuelMap.X0 + " Z0=" + BtcDuelMap.Z0 + " floor=" + BtcDuelMap.Floor +
                " void=" + BtcDuelMap.OnVoid + (BtcDuel.Pvp ? " pvp host=" + BtcDuel.HostNick.Replace(' ', '_') + " guest=" + BtcDuel.GuestNick.Replace(' ', '_') : ""));
        }

        private static string Fmt(int[] c)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < c.Length; i++) if (c[i] > 0) { if (sb.Length > 0) sb.Append('+'); sb.Append(BtcDuelConfig.UnitNames[i].Replace(' ', '_')).Append('x').Append(c[i]); }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        private void StartFight()
        {
            int left = 0; Labor[] ls = FindObjectsOfType<Labor>();
            for (int i = 0; i < ls.Length; i++) if (ls[i] != null && (Fac[0].IsSame(ls[i].gameObject) || Fac[1].IsSame(ls[i].gameObject))) left++;
            if (left > 0) BtcLog.Warn("Duel", "leftover units before spawn: " + left);
            SpawnSide(0, BtcDuelConfig.PlayerCount);
            SpawnSide(1, BtcDuelConfig.BotCount);
            BtcDuelLog.SetT0();
            if (Units.Count == 0) { BtcLog.Warn("Duel", "no units configured"); }
            AvgDmg[0] = AvgDmg[1] = 32f;
            int tier = (int)BtcDuelConfig.Difficulty;
            if (BtcDuel.Pvp) _bot = null;   // a human plays side 1
            else if (BtcDuelConfig.Difficulty == BtcDuelDifficulty.BreaktronZero && BtcDuelBotLuminaZero.EnsureLoaded()) _bot = new BtcDuelBotLuminaZero(this, 1);
            else
            {
                if (BtcDuelConfig.Difficulty == BtcDuelDifficulty.BreaktronZero) { tier = (int)BtcDuelDifficulty.Chosen; BtcDuelLog.Line("MODE", "BreaktronZero (B0) has no policy: plays as Chosen"); }
                _bot = new BtcDuelBot(this, 1, tier == 0 ? 0 : 1, tier, true);
            }
            _botP = BtcDuel.AutoPlayerLevel < 0 || BtcDuel.Pvp ? null : BtcDuel.AutoPlayerKind == 9 && BtcDuelBotLuminaZero.EnsureLoaded() ? new BtcDuelBotLuminaZero(this, 0) : ((BtcDuel.AutoPlayerKind >= 3 && BtcDuel.AutoPlayerKind <= 7) || BtcDuel.AutoPlayerKind == 10 || BtcDuel.AutoPlayerKind == 11 ? (IDuelBot)new BtcDuelHunter(this, 0, BtcDuel.AutoPlayerKind == 4, BtcDuel.AutoPlayerKind == 5, BtcDuel.AutoPlayerKind) : BtcDuel.AutoPlayerKind >= 2 ? (IDuelBot)new BtcDuelBot(this, 0, BtcDuel.AutoPlayerLevel, BtcDuel.AutoPlayerTier, true) : new BtcDuelBotV2(this, 0, BtcDuel.AutoPlayerLevel, BtcDuel.AutoPlayerTier, BtcDuel.AutoPlayerKind == 1));
            State = BtcDuelState.Fight;
            BtcDuelLog.Line("START", "units=" + Units.Count);
            LookAtArena();
            if (BtcDuel.Pvp) BtcDuelNet.SendNow();
        }

        private void SpawnSide(int side, int[] counts)
        {
            int total = BtcDuelConfig.Total(counts), idx = 0;
            Quaternion rot = Quaternion.Euler(0f, side == 0 ? 90f : -90f, 0f);
            for (int k = 0; k < counts.Length; k++)
                for (int c = 0; c < counts[k]; c++)
                {
                    BtcDuelUnit kind = (BtcDuelUnit)k;
                    GameObject go = BtcDuel.SpawnUnit(kind, Fac[side], BtcDuelMap.SpawnPoint(side, idx, total), rot);
                    idx++;
                    if (go == null) { BtcLog.Warn("Duel", "spawn failed " + kind); continue; }
                    BtcDuelUnitRef u = new BtcDuelUnitRef();
                    u.Id = _nextId++; u.Side = side; u.Kind = kind; u.Go = go; u.Tf = go.transform;
                    u.Labor = go.GetComponent<Labor>(); u.Dr = go.GetComponent<DamageReceiver>();
                    u.MaxHp = u.Dr != null ? u.Dr.MaxHP : 0f;
                    u.Zone = BtcDuelMap.ZoneAt(u.Tf.position.x, u.Tf.position.z);
                    u.PrevPos = u.Tf.position;
                    Units.Add(u); BtcDuel.ById[go.GetInstanceID()] = u;
                    if (kind == BtcDuelUnit.BarrelWorker) BtcDuel.GiveBarrel(go, Fac[side]);
                    Profession pf = go.GetComponent<Profession>();
                    BtcDuelLog.Line("UNIT", "u" + u.Id + " s" + side + " " + kind + " maxhp=" + u.MaxHp + " hp=" + (u.Dr != null ? u.Dr.CurrentHP : -1f) + " prof=" + (pf != null ? pf.GetType().Name + " lvl=" + pf.CharacterLevel : "?"));
                }
        }

        internal void DespawnAll()
        {
            for (int i = 0; i < Units.Count; i++) BtcDuel.Despawn(Units[i].Go);
            Units.Clear(); BtcDuel.ById.Clear();
            for (int i = 0; i < BtcDuel.Barrels.Count; i++) BtcDuel.Despawn(BtcDuel.Barrels[i]);
            BtcDuel.Barrels.Clear();
        }

        internal void Rematch()
        {
            if (State == BtcDuelState.Loading || State == BtcDuelState.Idle) return;
            BtcDuelLog.Line("REMATCH", "");
            if (BtcDuelConfig.ArenaScale != BtcDuelMap.Scale)
            {
                // another arena size: rebuild the plateau in place, then wait for the engine to settle
                DespawnAll();
                BtcDuelMap.SetScale(BtcDuelConfig.ArenaScale);
                bool ok = BtcDuelMap.OnVoid ? BtcDuelMap.BuildVoid(BtcDuelMap.CenterXi, BtcDuelMap.CenterZi) : BtcDuelMap.Build(Vector3.zero);
                if (!ok) { Deactivate("arena rebuild failed"); return; }
                State = BtcDuelState.Loading; _readySince = Time.realtimeSinceStartup - 5f; _builtAt = Time.realtimeSinceStartup; _calmSince = -1f;
                LookAtArena();
                return;
            }
            StartCountdown();
        }

        internal void ExitToMenu()
        {
            Deactivate("exit button");
            try { Architecte.QuitToMainMenu(); } catch (Exception e) { BtcLog.Error("Duel", "quit", e); }
        }

        // ================= PvP round control =================
        /// <summary>PvP: side wants the next round; it starts when both sides pressed it (host only; the opponent's press arrives as msg 136).</summary>
        internal void WantNextRound(int side)
        {
            if (BtcDuel.IsClient || side < 0 || side > 1) return;
            if (State != BtcDuelState.Over) return;
            if (BtcDuel.WantNext[side]) return;
            BtcDuel.WantNext[side] = true;
            BtcDuelLog.Line("MODE", "next round wanted by s" + side);
            if (_gone) { BtcDuel.WantNext[side] = false; return; }
            if (BtcDuel.WantNext[0] && BtcDuel.WantNext[1]) Rematch();
            else BtcDuelNet.SendNow();
        }

        /// <summary>Instant reset: the side that pressed it loses the running round (counted only once the fight started), a new round starts at once.</summary>
        internal void Surrender(int side)
        {
            if (BtcDuel.IsClient || side < 0 || side > 1) return;
            if (State != BtcDuelState.Fight && State != BtcDuelState.Countdown && State != BtcDuelState.Over) return;
            string who = side == 0 ? (BtcDuel.Pvp ? BtcDuel.HostNick : "player") : (BtcDuel.Pvp ? BtcDuel.GuestNick : "bot");
            BtcDuelLog.Line("MODE", "reset by s" + side);
            if (State == BtcDuelState.Fight) Finish(1 - side, "surrender (" + who + ")");
            if (_gone) return;
            Rematch();
        }

        private void Deactivate(string why)
        {
            BtcLog.Info("Duel", "deactivate: " + why);
            BtcDuelLog.Line("END", "deactivate " + why);
            BtcDuelLog.End();
            BtcDuel.Active = false;
            BtcDuel.ClearPvp();
            State = BtcDuelState.Idle;
            Units.Clear(); BtcDuel.ById.Clear();
            _bot = null; _botP = null; _gone = false;
        }

        // ================= client mode (PvP opponent / spectator) =================
        /// <summary>State message from the host (BtcDuelNet). Fields mirror the host's match.</summary>
        internal void ApplyState(BtcDuelState st, float countdown, int winner, string reason, float c0, float c1, bool e0, bool e1, int matchNo, float fightTime,
            int k0, int k1, int cx, int cz, int scale, bool w0, bool w1, int seq, int lastWinner, string lastReason, float lastTime, int lastK0, int lastK1)
        {
            State = st; CountdownLeft = countdown; Winner = winner; Reason = reason; Ctrl[0] = c0; Ctrl[1] = c1; Escaping[0] = e0; Escaping[1] = e1;
            MatchNo = matchNo; FightTime = fightTime; Kills[0] = k0; Kills[1] = k1;
            _gCx = cx; _gCz = cz; _gScale = scale;
            BtcDuel.WantNext[0] = w0; BtcDuel.WantNext[1] = w1;
            if (_seqSeen < 0) _seqSeen = seq;   // results before this game joined are not ours to count
            else if (seq > _seqSeen)
            {
                _seqSeen = seq;
                LastWinner = lastWinner; LastReason = lastReason; LastTime = lastTime; LastK0 = lastK0; LastK1 = lastK1;
                if (BtcDuel.LocalSide >= 0 && lastWinner >= 0) { if (lastWinner == BtcDuel.LocalSide) BtcDuel.Wins++; else BtcDuel.Losses++; }
                BtcDuelPvp.RoundOver(lastWinner, lastReason, lastTime, lastK0, lastK1);
            }
        }

        private void ClientTick()
        {
            if (!NetworkClient.active || SceneManager.GetActiveScene().buildIndex != 2) { Deactivate("client session ended"); return; }
            if (State == BtcDuelState.Countdown) CountdownLeft -= Time.deltaTime;
            if (State == BtcDuelState.Fight) FightTime += Time.deltaTime;
            if (_gScale < 1 || (_bCx == _gCx && _bCz == _gCz && _bScale == _gScale)) return;
            try { if (!VoxelEngine.Ready || !Architecte.commence) return; } catch (Exception) { return; }
            BtcDuelMap.SetScale(_gScale);
            if (!BtcDuelMap.BuildVoid(_gCx, _gCz)) { _bCx = _gCx; _bCz = _gCz; _bScale = _gScale; return; }   // logged; do not retry every frame
            _bCx = _gCx; _bCz = _gCz; _bScale = _gScale;
            ClientRelations();
            if (!_clientLooked) { _clientLooked = true; LookAtArena(); }
        }

        /// <summary>Client: the two playing factions are enemies on this side too (attack orders on the other player's units).</summary>
        private static void ClientRelations()
        {
            try
            {
                Faction a = null, b = null;
                foreach (User u in BtcRegistry.UsersArray())
                {
                    if (u == null || u.faction == null || u.faction.IsSpectator || u.Network_screenName == "REC") continue;
                    if (a == null) a = u.faction; else if (u.faction != a) { b = u.faction; break; }
                }
                if (a == null || b == null) return;
                a.SetFactionRelation(b, Affiliation.Relation.Enemy);
                b.SetFactionRelation(a, Affiliation.Relation.Enemy);
            }
            catch (Exception e) { BtcLog.Warn("Duel", "client relation: " + e.Message); }
        }

        // ================= fight tick =================
        private float _rtDiag, _fpsAcc;

        private void TickFight(float dt)
        {
            _fpsAcc += dt;
            if (_fpsAcc >= 1f) { _fpsAcc = 0f; BtcDuelLog.Line("FPS", "fps=" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0.0") + " fixed=" + Time.fixedDeltaTime.ToString("0.0000")); }
            if (Time.realtimeSinceStartup - _rtDiag > 5f) { _rtDiag = Time.realtimeSinceStartup; BtcDuelLog.Line("DIAG", "timeScale=" + Time.timeScale + " rt=" + Time.realtimeSinceStartup.ToString("0") + " fps=" + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("0")); }
            FightTime += dt;
            _rulesAcc += dt; _healAcc += dt; _snapAcc += dt;
            if (BtcDuel.LabDodgeMs > 0f) DodgeTick();
            if (FightTime > 1.2f)                      // units are still settling (landing stun swallows orders)
            {
                if (_bot != null) _bot.Tick(dt);
                if (_botP != null) _botP.Tick(dt);
            }
            if (_healAcc >= HealStep) { if (BtcDuelConfig.InfiniteHeal) HealTick(); _healAcc -= HealStep; }
            if (_rulesAcc >= RulesStep) { RulesTick(_rulesAcc); _rulesAcc = 0f; }
            if (_snapAcc >= 0.2f) { Snapshot(); _snapAcc = 0f; }
        }

        private void DodgeTick()
        {
            float now = Time.time;
            for (int i = 0; i < Units.Count; i++)
            {
                BtcDuelUnitRef u = Units[i];
                if (u.DodgeAt < 0f || now < u.DodgeAt || u.Dead) continue;
                u.DodgeAt = -1f;
                Vector3 p = u.Tf.position;
                BtcDuelLog.Line("DODGE", "u" + u.Id + " steps away");
                BtcDuel.Move(u, new Vector3(p.x + (u.Side == 0 ? -3f : 3f), BtcDuelMap.Floor, p.z));
            }
        }

        /// <summary>Infinite heal: +HealPerStep hp to every living unit once per HealStep seconds of fight time.</summary>
        private void HealTick()
        {
            float amount = HealPerStep;
            for (int i = 0; i < Units.Count; i++)
            {
                BtcDuelUnitRef u = Units[i];
                if (u.Dead || u.Dr == null) continue;
                float room = u.MaxHp - u.Dr.CurrentHP;
                if (room > 0.01f) u.Dr.Heal(Mathf.Min(amount, room));
            }
        }

        private void RulesTick(float dt)
        {
            int[] inMid = new int[2];
            int[] alive = new int[2];
            int[] inSafe = new int[2];
            for (int i = 0; i < Units.Count; i++)
            {
                BtcDuelUnitRef u = Units[i];
                if (u.Dead) continue;
                if (u.Go == null || u.Tf == null || u.Dr == null) { MarkDead(u, null, "vanished"); continue; }
                if (BtcDuelMap.OnVoid && u.Tf.position.y < BtcDuelMap.Floor - 6f)
                {
                    // walked off the slab: falling into the void = death (the other side gets the kill)
                    MarkDead(u, null, "fell off the arena");
                    BtcDuel.Despawn(u.Go); u.Go = null;
                    if (Winner >= 0) return;
                    continue;
                }
                BtcDuelZone z = BtcDuelMap.ZoneAt(u.Tf.position.x, u.Tf.position.z);
                { Vector3 pp = u.Tf.position; float ivl = Mathf.Max(0.02f, dt); u.Vel = Vector3.Lerp(u.Vel, (pp - u.PrevPos) / ivl, 0.5f); u.PrevPos = pp; if (u.Vel.sqrMagnitude > 0.25f) u.MovedAt = Time.time; }
                if (z != u.Zone) { BtcDuelLog.Line("ZONE", "u" + u.Id + " s" + u.Side + " " + u.Zone + "->" + z); u.Zone = z; }
                alive[u.Side]++;
                if (z == BtcDuelZone.Mid) inMid[u.Side]++;
                if (z == (u.Side == 0 ? BtcDuelZone.SafeA : BtcDuelZone.SafeB)) inSafe[u.Side]++;
                if (z == (u.Side == 0 ? BtcDuelZone.SafeB : BtcDuelZone.SafeA)) { Finish(1 - u.Side, "enemy unit u" + u.Id + " entered the safe zone"); return; }
            }
            if (Winner >= 0) return;
            if (alive[0] == 0 && alive[1] == 0) { Finish(-1, "draw, everyone dead"); return; }
            if (alive[0] == 0) { Finish(1, "all enemy units dead"); return; }
            if (alive[1] == 0) { Finish(0, "all enemy units dead"); return; }
            // kill and escape: the escaping side wins when every living unit stands in its own safe zone
            for (int s = 0; s < 2; s++)
                if (Escaping[s] && inSafe[s] == alive[s]) { Finish(s, "kill and escape"); return; }
            // space control only while nobody is escaping
            if (!Escaping[0] && !Escaping[1])
            {
                for (int s = 0; s < 2; s++)
                {
                    bool holds = inMid[s] > 0 && inMid[1 - s] == 0;
                    float prev = Ctrl[s];
                    Ctrl[s] = holds ? Ctrl[s] + dt : 0f;
                    if (holds && prev <= 0f) BtcDuelLog.Line("MODE", "space s" + s + " start");
                    if (!holds && prev > 0f) BtcDuelLog.Line("MODE", "space s" + s + " lost after " + prev.ToString("0.0"));
                    if (Ctrl[s] >= SpaceControlSec) { Finish(s, "space control"); return; }
                }
            }
            else { Ctrl[0] = Ctrl[1] = 0f; }
        }

        private void Snapshot()
        {
            _sb.Length = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                BtcDuelUnitRef u = Units[i];
                if (u.Dead || u.Tf == null || u.Dr == null) continue;
                Vector3 p = u.Tf.position;
                _sb.Append(u.Id).Append(':').Append(u.Side).Append(':').Append(p.x.ToString("0.0")).Append(',').Append(p.z.ToString("0.0")).Append(':').Append((int)u.Dr.CurrentHP).Append(' ');
            }
            BtcDuelLog.Line("SNAP", _sb.ToString());
        }

        // ================= events from the patches =================
        internal void OnOrder(GameObject unit, Task task, Labor.DecisionSource source)
        {
            if (State != BtcDuelState.Fight) return;
            BtcDuelUnitRef u;
            if (!BtcDuel.TryGet(unit, out u)) return;
            BtcDuelUnitRef t = null;
            if (task != null && task.Target != null) BtcDuel.TryGet(task.Target, out t);
            if (task != null)     // auto-acquired targets of an attack-move never pass through SendOrder (ORD logs), so every order here was given by the owner
            {
                // the bot reads what kind of order the enemy gave: an attack-move auto-target is not a focus (anti-sweep)
                string d = task.Description;
                u.OrdKind = t != null ? 2 : (d != null && (d.StartsWith("MoveAttack") || d.StartsWith("HoldPositionAndAttack")) ? 1 : 3);
                u.OrdTargetId = t != null ? t.Id : -1;
                u.OrdAt = Time.time;
                Vector3 od = Vector3.zero; u.OrdHasDest = t == null && TryOrderDest(task, out od); if (u.OrdHasDest) u.OrdDest = od;
            }
            if ((u.Side != 0 && !BtcDuel.Pvp) || BtcDuel.AutoPlayerLevel >= 0) return;
            Vector3 p = u.Tf.position;
            BtcDuelLog.Line("ORD", "u" + u.Id + (BtcDuel.Pvp ? " s" + u.Side : "") + " " + (task != null ? task.Description : "?") + (t != null ? " >u" + t.Id : "") + " at=" + p.x.ToString("0.0") + "," + p.z.ToString("0.0") + " hp=" + (int)u.Dr.CurrentHP + OrderDest(task));
        }

        /// <summary>" to=x,z" for position-based orders (Task.InitialPackage value is a Location), else empty.</summary>
        internal static string OrderDest(Task task)
        {
            Vector3 d;
            return TryOrderDest(task, out d) ? " to=" + d.x.ToString("0.0") + "," + d.z.ToString("0.0") : "";
        }

        /// <summary>Destination of a position-based order (Task.InitialPackage value is a Location).</summary>
        internal static bool TryOrderDest(Task task, out Vector3 d)
        {
            d = Vector3.zero;
            try
            {
                IOrderPackageValue v = task != null ? task.InitialPackage as IOrderPackageValue : null;
                Location l = v != null ? v.value as Location : null;
                if (l == null) return false;
                d = l.Position;
                return true;
            }
            catch (Exception) { return false; }
        }

        internal void OnStrike(BtcDuelUnitRef u, GameObject target)
        {
            if (State != BtcDuelState.Fight) return;
            float now = Time.time;
            BtcDuelUnitRef t; BtcDuel.TryGet(target, out t);
            BtcDuelLog.Line("STRIKE", "u" + u.Id + ">" + (t != null ? "u" + t.Id : "?") + " since=" + (int)((now - u.StrikeAt) * 1000f));
            u.StrikeAt = now; u.StrikeTarget = target;
            if (BtcDuel.LabDodgeMs > 0f && t != null && !t.Dead) t.DodgeAt = now + BtcDuel.LabDodgeMs * 0.001f;
            if (_bot != null) _bot.OnStrike(u);
            if (_botP != null) _botP.OnStrike(u);
        }

        internal void OnDamage(BtcDuelUnitRef victim, GameObject attackerGo, float dealt, float hpAfter)
        {
            if (State != BtcDuelState.Fight || victim == null || victim.Dead) return;
            BtcDuelUnitRef att;
            BtcDuel.TryGet(attackerGo, out att);
            if (dealt > 0.5f) { victim.LastHurtAt = Time.time; victim.HitT[victim.HitN++ & 3] = Time.time; }
            if (att != null && att.Side != victim.Side)
            {
                float now = Time.time;
                float gap = att.LastHitAt > 0f ? (now - att.LastHitAt) * 1000f : 0f;
                bool skip = att.LastHitAt > 0f && gap < BtcDuel.VanillaCycleMs * 0.9f;
                att.LastHitAt = now;
                _hits[att.Side]++; if (skip) _skips[att.Side]++;
                if (dealt > 0.5f) AvgDmg[att.Side] = AvgDmg[att.Side] * 0.9f + dealt * 0.1f;
                BtcDuelLog.Line("HIT", "u" + att.Id + ">u" + victim.Id + " dmg=" + dealt.ToString("0.0") + " hp=" + hpAfter.ToString("0") + " dist=" + Vector3.Distance(att.Tf.position, victim.Tf.position).ToString("0.00") + " gap=" + (int)gap + " D=" + (int)((now - att.StrikeAt) * 1000f) + (skip ? " SKIP" : ""));
                // hitting a unit that stands in its own safe zone is an instant loss
                BtcDuelZone vz = BtcDuelMap.ZoneAt(victim.Tf.position.x, victim.Tf.position.z);
                if (vz == (victim.Side == 0 ? BtcDuelZone.SafeA : BtcDuelZone.SafeB)) { Finish(victim.Side, "unit u" + att.Id + " attacked a unit in the safe zone"); return; }
            }
            if (hpAfter <= 0f) MarkDead(victim, att, "killed");
        }

        private void MarkDead(BtcDuelUnitRef victim, BtcDuelUnitRef killer, string how)
        {
            if (victim.Dead) return;
            victim.Dead = true;
            int ks = killer != null ? killer.Side : 1 - victim.Side;
            _kills[ks]++;
            BtcDuelLog.Line("KILL", "u" + victim.Id + " s" + victim.Side + " by " + (killer != null ? "u" + killer.Id : "?") + " (" + how + ")");
            if (Winner >= 0) return;
            // the side that has killed more units escapes; equal losses = no escape, play continues
            bool e0 = _kills[0] > _kills[1], e1 = _kills[1] > _kills[0];
            if (e0 != Escaping[0] || e1 != Escaping[1])
            {
                Escaping[0] = e0; Escaping[1] = e1;
                BtcDuelLog.Line("MODE", e0 ? "side 0 escapes (kills " + _kills[0] + "/" + _kills[1] + ")" : (e1 ? "side 1 escapes (kills " + _kills[0] + "/" + _kills[1] + ")" : "equal losses, escapes reset"));
            }
        }

        private void Finish(int winner, string why)
        {
            if (Winner >= 0 || State != BtcDuelState.Fight) return;
            Winner = winner; Reason = why; State = BtcDuelState.Over;
            if (winner == 0) BtcDuel.Wins++; else if (winner == 1) BtcDuel.Losses++;   // session score (Online tab / Discord status)
            BtcDuelLog.Line("END", "winner=" + (winner < 0 ? "none" : (winner == 0 ? "player" : "bot")) + " reason=" + why + " t=" + FightTime.ToString("0.0") +
                " hits=" + _hits[0] + "/" + _hits[1] + " skips=" + _skips[0] + "/" + _skips[1] + " kills=" + _kills[0] + "/" + _kills[1]);
            BtcDuelLog.End();
            BtcLog.Info("Duel", "match " + MatchNo + " over: " + (winner < 0 ? "draw" : (winner == 0 ? "player" : "bot")) + " (" + why + ") log=" + BtcDuelLog.CurrentPath);
            if (BtcDuel.Pvp)
            {
                LastWinner = winner; LastReason = why; LastTime = FightTime; LastK0 = _kills[0]; LastK1 = _kills[1];
                ResultSeq++;
                BtcDuel.WantNext[0] = BtcDuel.WantNext[1] = false;
                BtcDuelPvp.RoundOver(winner, why, FightTime, _kills[0], _kills[1]);
                BtcDuelNet.SendNow();
            }
        }

        // ================= HUD =================
        private GUIStyle _big, _small;

        private void OnGUI()
        {
        	if (useGUILayout) useGUILayout = false; // no GUILayout here: skip Unity's per-event layout pass (garbage)
            if (!BtcDuel.Active || State == BtcDuelState.Idle) return;
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label); _big.alignment = TextAnchor.MiddleCenter; _big.fontSize = 28; _big.fontStyle = FontStyle.Bold; _big.normal.textColor = Color.white;
                _small = new GUIStyle(GUI.skin.label); _small.alignment = TextAnchor.MiddleCenter; _small.fontSize = 15; _small.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }
            float w = Screen.width, cx = w * 0.5f;
            bool pvp = BtcDuel.Pvp;
            int me = pvp ? BtcDuel.LocalSide : 0;          // -1 = spectator: names instead of "you"
            int view = me < 0 ? 0 : me, other = 1 - view;
            string nameV = pvp ? (view == 0 ? BtcDuel.HostNick : BtcDuel.GuestNick) : "", nameO = pvp ? (other == 0 ? BtcDuel.HostNick : BtcDuel.GuestNick) : BtcGameplay.T("Bot", "Бот");
            string head = "";
            switch (State)
            {
                case BtcDuelState.Loading: head = BtcGameplay.T("Preparing the arena...", "Подготовка арены..."); break;
                case BtcDuelState.Countdown: head = Mathf.CeilToInt(Mathf.Max(0f, CountdownLeft)).ToString(); break;
                case BtcDuelState.Over:
                    if (Winner < 0) head = BtcGameplay.T("Draw", "Ничья");
                    else if (me < 0) head = (Winner == 0 ? BtcDuel.HostNick : BtcDuel.GuestNick) + BtcGameplay.T(" won", " победил");
                    else head = Winner == me ? BtcGameplay.T("You won", "Победа") : (pvp ? nameO + BtcGameplay.T(" won", " победил") : BtcGameplay.T("Bot won", "Победил бот"));
                    break;
            }
            float y0 = 70f;
            if (pvp)
            {
                // "Host 2 : 1 Guest" (session) + the players' own score line
                GUI.Label(new Rect(cx - 350f, 40f, 700f, 26f), BtcDuel.HostNick + "   vs   " + BtcDuel.GuestNick + (MatchNo > 0 ? "   ·   " + BtcGameplay.T("round ", "раунд ") + MatchNo : ""), _small);
                y0 = 96f;
            }
            if (head.Length > 0) GUI.Label(new Rect(cx - 250f, y0, 500f, 50f), head, _big);
            if (State == BtcDuelState.Over) GUI.Label(new Rect(cx - 300f, y0 + 48f, 600f, 26f), Reason, _small);
            if (State == BtcDuelState.Fight)
            {
                string a = me < 0 ? nameV : BtcGameplay.T("You", "Ты"), b = nameO;
                string info = Escaping[view] ? (me < 0 ? a + BtcGameplay.T(" is escaping", " уходит (kill & escape)") : BtcGameplay.T("You are escaping", "Ты уходишь (kill & escape)"))
                    : (Escaping[other] ? b + BtcGameplay.T(" is escaping", " уходит") + (me < 0 ? "" : BtcGameplay.T(": kill someone!", ": убей кого-нибудь!")) : "");
                if (Ctrl[view] > 0f) info = (me < 0 ? a + ": " : "") + BtcGameplay.T("Space control: ", "Контроль мида: ") + Ctrl[view].ToString("0.0") + "/10";
                else if (Ctrl[other] > 0f) info = b + BtcGameplay.T(" holds the middle: ", " держит мид: ") + Ctrl[other].ToString("0.0") + "/10";
                if (info.Length > 0) GUI.Label(new Rect(cx - 300f, y0, 600f, 30f), info, _small);
            }
            if (pvp && me >= 0)
            {
                string sc = BtcDuelPvp.ScoreLine();
                if (sc.Length > 0) GUI.Label(new Rect(cx - 350f, Screen.height - 64f, 700f, 24f), sc, _small);
            }
            float bw = 150f, bh = 28f, by = 8f;
            if (State == BtcDuelState.Loading) return;
            if (!pvp)
            {
                if (GUI.Button(new Rect(cx - bw - 4f, by, bw, bh), BtcGameplay.T("Rematch", "Реванш"))) Rematch();
                if (GUI.Button(new Rect(cx + 4f, by, bw, bh), BtcGameplay.T("Exit", "Выход"))) ExitToMenu();
                return;
            }
            if (me < 0)
            {
                if (GUI.Button(new Rect(cx - bw * 0.5f, by, bw, bh), BtcGameplay.T("Exit", "Выход"))) ExitToMenu();
                return;
            }
            if (State == BtcDuelState.Over)
            {
                int n = (BtcDuel.WantNext[0] ? 1 : 0) + (BtcDuel.WantNext[1] ? 1 : 0);
                string lbl = BtcDuel.WantNext[me] ? BtcGameplay.T("Waiting ", "Ждём ") + n + "/2" : BtcGameplay.T("Next round ", "Ещё раунд ") + n + "/2";
                if (GUI.Button(new Rect(cx - bw - 4f, by, bw, bh), lbl) && !BtcDuel.WantNext[me]) { if (BtcDuel.IsClient) BtcDuelNet.SendAction("next"); else WantNextRound(0); }
            }
            else if (GUI.Button(new Rect(cx - bw - 4f, by, bw, bh), BtcGameplay.T("Give up + reset", "Сдаться и заново")))
            {
                if (BtcDuel.IsClient) BtcDuelNet.SendAction("reset"); else Surrender(0);
            }
            if (GUI.Button(new Rect(cx + 4f, by, bw, bh), BtcGameplay.T("Exit", "Выход"))) ExitToMenu();
        }
    }
}
