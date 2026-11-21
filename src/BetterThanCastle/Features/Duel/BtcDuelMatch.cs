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
        private readonly int[] _hits = new int[2], _skips = new int[2], _kills = new int[2];
        private readonly StringBuilder _sb = new StringBuilder(512);
        private IDuelBot _bot, _botP;
        internal readonly float[] AvgDmg = { 32f, 32f };
        private Vector2 _scroll;

        internal void BeginLoad()
        {
            State = BtcDuelState.Loading;
            _readySince = -1f; _builtAt = -1f;
            Units.Clear(); BtcDuel.ById.Clear();
        }

        // ================= update =================
        private void Update()
        {
            if (!BtcDuel.Active) return;
            try
            {
                switch (State)
                {
                    case BtcDuelState.Loading: TickLoading(); break;
                    case BtcDuelState.Countdown:
                        CountdownLeft -= Time.deltaTime;
                        if (CountdownLeft <= 0f) StartFight();
                        break;
                    case BtcDuelState.Fight: TickFight(Time.deltaTime); break;
                }
                if (State != BtcDuelState.Loading && State != BtcDuelState.Idle && !NetworkServer.active) Deactivate("session ended");
            }
            catch (Exception e) { BtcLog.Error("Duel", "Update", e); }
        }

        private void TickLoading()
        {
            if (!Ready()) { _readySince = -1f; return; }
            if (_readySince < 0f) _readySince = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _readySince < 2f) return;
            if (_builtAt >= 0f) { SettleStep(); return; }
            Vector3 anchor = Anchor();
            BtcDuelMap.SetScale(BtcDuelConfig.ArenaScale);
            if (!BtcDuelMap.Build(anchor)) { BtcLog.Error("Duel", "arena build failed, mode aborted"); Deactivate("arena build failed"); return; }
            BtcLog.Info("Duel", "arena " + BtcDuelMap.Describe());
            if (!FindFactions()) { Deactivate("no second faction"); return; }
            ClearForeignUnits();
            LookAtArena();
            _builtAt = Time.realtimeSinceStartup; _calmSince = -1f;
        }

        /// <summary>Terrain/navigation rebuild after the arena edit is heavy: wait until the frame rate recovers (max 90 s) before the countdown.</summary>
        private void SettleStep()
        {
            float now = Time.realtimeSinceStartup;
            bool calm = Time.unscaledDeltaTime < 0.03f;
            if (!calm) _calmSince = -1f; else if (_calmSince < 0f) _calmSince = now;
            if ((_calmSince >= 0f && now - _calmSince > 3f) || now - _builtAt > 90f)
            {
                BtcLog.Info("Duel", "arena settled after " + (now - _builtAt).ToString("0.0") + " s");
                _builtAt = -1f;
                StartCountdown();
            }
        }

        private static bool Ready()
        {
            try
            {
                if (!Architecte.commence || !Architecte.physicsReady) return false;
                User u = User.LocalUser;
                return u != null && u.faction != null && Anchor() != Vector3.zero;
            }
            catch (Exception) { return false; }
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
            for (int i = 0; i < all.Count; i++)
            {
                Faction f = all[i];
                if (f == null || f == Fac[0] || f.HasOwner() || f.IsSpectator || f.isAI || f == Faction.DefaultNeutral) continue;
                Fac[1] = f; break;
            }
            if (Fac[1] == null) { BtcLog.Error("Duel", "no free faction for the bot (factions=" + all.Count + ")"); return false; }
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
            State = BtcDuelState.Countdown;
            CountdownLeft = CountdownSec;
            BtcDuelLog.Begin();
            BtcDuelLog.Line("CFG", "match=" + MatchNo + " diff=" + BtcDuelConfig.Difficulty + " skip=" + BtcDuelConfig.SkipAnims + " heal=" + BtcDuelConfig.InfiniteHeal +
                " player=" + Fmt(BtcDuelConfig.PlayerCount) + " bot=" + Fmt(BtcDuelConfig.BotCount) + " arenaX0=" + BtcDuelMap.X0 + " Z0=" + BtcDuelMap.Z0 + " floor=" + BtcDuelMap.Floor);
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
            if (BtcDuelConfig.Difficulty == BtcDuelDifficulty.BreaktronZero && BtcDuelBotLuminaZero.EnsureLoaded()) _bot = new BtcDuelBotLuminaZero(this, 1);
            else
            {
                if (BtcDuelConfig.Difficulty == BtcDuelDifficulty.BreaktronZero) { tier = (int)BtcDuelDifficulty.Chosen; BtcDuelLog.Line("MODE", "BreaktronZero (B0) has no policy: plays as Chosen"); }
                _bot = new BtcDuelBot(this, 1, tier == 0 ? 0 : 1, tier, true);
            }
            _botP = BtcDuel.AutoPlayerLevel < 0 ? null : BtcDuel.AutoPlayerKind == 9 && BtcDuelBotLuminaZero.EnsureLoaded() ? new BtcDuelBotLuminaZero(this, 0) : ((BtcDuel.AutoPlayerKind >= 3 && BtcDuel.AutoPlayerKind <= 7) || BtcDuel.AutoPlayerKind == 10 || BtcDuel.AutoPlayerKind == 11 ? (IDuelBot)new BtcDuelHunter(this, 0, BtcDuel.AutoPlayerKind == 4, BtcDuel.AutoPlayerKind == 5, BtcDuel.AutoPlayerKind) : BtcDuel.AutoPlayerKind >= 2 ? (IDuelBot)new BtcDuelBot(this, 0, BtcDuel.AutoPlayerLevel, BtcDuel.AutoPlayerTier, true) : new BtcDuelBotV2(this, 0, BtcDuel.AutoPlayerLevel, BtcDuel.AutoPlayerTier, BtcDuel.AutoPlayerKind == 1));
            State = BtcDuelState.Fight;
            BtcDuelLog.Line("START", "units=" + Units.Count);
            LookAtArena();
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
                if (!BtcDuelMap.Build(Vector3.zero)) { Deactivate("arena rebuild failed"); return; }
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

        private void Deactivate(string why)
        {
            BtcLog.Info("Duel", "deactivate: " + why);
            BtcDuelLog.Line("END", "deactivate " + why);
            BtcDuelLog.End();
            BtcDuel.Active = false;
            State = BtcDuelState.Idle;
            Units.Clear(); BtcDuel.ById.Clear();
            _bot = null; _botP = null;
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
            if (u.Side != 0 || BtcDuel.AutoPlayerLevel >= 0) return;
            Vector3 p = u.Tf.position;
            BtcDuelLog.Line("ORD", "u" + u.Id + " " + (task != null ? task.Description : "?") + (t != null ? " >u" + t.Id : "") + " at=" + p.x.ToString("0.0") + "," + p.z.ToString("0.0") + " hp=" + (int)u.Dr.CurrentHP + OrderDest(task));
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
            BtcDuelLog.Line("END", "winner=" + (winner < 0 ? "none" : (winner == 0 ? "player" : "bot")) + " reason=" + why + " t=" + FightTime.ToString("0.0") +
                " hits=" + _hits[0] + "/" + _hits[1] + " skips=" + _skips[0] + "/" + _skips[1] + " kills=" + _kills[0] + "/" + _kills[1]);
            BtcDuelLog.End();
            BtcLog.Info("Duel", "match " + MatchNo + " over: " + (winner < 0 ? "draw" : (winner == 0 ? "player" : "bot")) + " (" + why + ") log=" + BtcDuelLog.CurrentPath);
        }

        // ================= HUD =================
        private GUIStyle _big, _small;

        private void OnGUI()
        {
            if (!BtcDuel.Active || State == BtcDuelState.Idle) return;
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label); _big.alignment = TextAnchor.MiddleCenter; _big.fontSize = 28; _big.fontStyle = FontStyle.Bold; _big.normal.textColor = Color.white;
                _small = new GUIStyle(GUI.skin.label); _small.alignment = TextAnchor.MiddleCenter; _small.fontSize = 15; _small.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }
            float w = Screen.width, cx = w * 0.5f;
            string head = "";
            switch (State)
            {
                case BtcDuelState.Loading: head = BtcGameplay.T("Preparing the arena...", "Подготовка арены..."); break;
                case BtcDuelState.Countdown: head = Mathf.CeilToInt(Mathf.Max(0f, CountdownLeft)).ToString(); break;
                case BtcDuelState.Over:
                    head = Winner < 0 ? BtcGameplay.T("Draw", "Ничья") : (Winner == 0 ? BtcGameplay.T("You won", "Победа") : BtcGameplay.T("Bot won", "Победил бот"));
                    break;
            }
            if (head.Length > 0) GUI.Label(new Rect(cx - 250f, 70f, 500f, 50f), head, _big);
            if (State == BtcDuelState.Over) GUI.Label(new Rect(cx - 300f, 118f, 600f, 26f), Reason, _small);
            if (State == BtcDuelState.Fight)
            {
                string info = Escaping[0] ? BtcGameplay.T("You are escaping", "Ты уходишь (kill & escape)") : (Escaping[1] ? BtcGameplay.T("Bot is escaping: kill someone!", "Бот уходит: убей кого-нибудь!") : "");
                if (Ctrl[0] > 0f) info = BtcGameplay.T("Space control: ", "Контроль мида: ") + Ctrl[0].ToString("0.0") + "/10";
                else if (Ctrl[1] > 0f) info = BtcGameplay.T("Enemy space control: ", "Бот держит мид: ") + Ctrl[1].ToString("0.0") + "/10";
                if (info.Length > 0) GUI.Label(new Rect(cx - 300f, 70f, 600f, 30f), info, _small);
            }
            float bw = 110f, bh = 28f, by = 8f;
            if (State != BtcDuelState.Loading)
            {
                if (GUI.Button(new Rect(cx - bw - 4f, by, bw, bh), BtcGameplay.T("Rematch", "Реванш"))) Rematch();
                if (GUI.Button(new Rect(cx + 4f, by, bw, bh), BtcGameplay.T("Exit", "Выход"))) ExitToMenu();
            }
        }
    }
}
