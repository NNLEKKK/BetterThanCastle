using System;
using System.Collections.Generic;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Input;
using Brix.Lua;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    internal enum BtcAimState { Idle = 0, Loading, Countdown, Run, Over }

    /// <summary>
    /// Aim trainer (Practice tab): sandbox on the default map, a 1-voxel floor high in the sky (over the void next to the island when
    /// there is room), enemy halberdiers on it, a fixed camera. Focus game: one target drops to 25% HP, right click it. Most wounded game:
    /// several drop to different HP (step %), right click the lowest one. Offline only, nothing on the wire.
    /// The floor is built ONCE at the largest size any setting needs, so Again / new settings only respawn the targets (no terrain work).
    /// </summary>
    internal sealed class BtcAim : MonoBehaviour
    {
        internal static bool Active;
        internal static BtcAim Run;
        /// <summary>Read by AimCameraLockPatch every CameraScript.Update.</summary>
        internal static bool CameraLocked;
#if DEV
        /// <summary>DEV lab: click the right target this many ms after each round starts (0 = off).</summary>
        internal static float DevAutoMs;
#endif

        internal const float CountdownSec = 3f;
        internal const int FloorTop = 235;
        /// <summary>Floor side for the largest grid (24 targets, spacing 8) plus fast strafe room and a 3-block margin.</summary>
        internal static readonly int Side = Mathf.CeilToInt(4 * BtcAimConfig.MaxSpacing + 2f * (PackRoom + 3f)) | 1;
        /// <summary>How far a target (strafe/wander) or the whole pack may leave its spot, in blocks.</summary>
        internal const float Amp = 2f, PackRoom = 6f;

        internal BtcAimState State = BtcAimState.Idle;
        internal float CountdownLeft, TimeLeft, Elapsed;
        internal BtcAimScore.Result Last;
        internal int Hits, Misses;
        /// <summary>Unit width in blocks measured from the colliders of the first spawned target (fallback 0.9).</summary>
        internal static float UnitWidth = 0.9f;
        internal static float Fov = 60f;

        private sealed class Target
        {
            internal GameObject Go; internal Transform Tf; internal Labor Labor; internal DamageReceiver Dr;
            internal Vector3 Home, Dest; internal float NextMoveAt, RestUntil = -1f; internal int Dir = 1;
            /// <summary>HP share this target is held at (1 = full).</summary>
            internal float Want = 1f;
        }

        private readonly List<Target> _t = new List<Target>();
        private Faction _me, _enemy;
        private float _minWant = 1f;
        private int _wounded;
        private float _roundAt, _roundD, _roundW;
        private Vector3 _center;
        private bool _built;
        private float _readySince = -1f, _builtAt = -1f, _calmSince = -1f, _sweepAt;
        private float _farWas = -1f;
        private bool _fogWas;
        private BtcAimConfig.Snap _cfg;
        private readonly System.Random _rng = new System.Random();
        private CameraScript _cs;
        private readonly List<int> _pick = new List<int>();

        // ================= launch =================
        internal static bool Launch()
        {
            if (Active || BtcDuel.Active) return false;
            BtcAimConfig.Save();
            _onVoid = BtcAimConfig.EmptyMap && BtcAimMap.Ensure();
            if (!(_onVoid ? BtcDuelLaunch.PickSandbox(BtcAimMap.Codename) : BtcDuelLaunch.PickSandbox())) return false;
            Active = true;
            if (Run == null)
            {
                GameObject go = new GameObject("BtcAim");
                DontDestroyOnLoad(go);
                Run = go.AddComponent<BtcAim>();
            }
            Run.BeginLoad();
            GameParam.StartGame(null);
            BtcLog.Info("Aim", "launch " + BtcAimConfig.Key(BtcAimConfig.Take()));
            return true;
        }

        private static bool _onVoid;
        private float _loadAt;

        private void BeginLoad()
        {
            State = BtcAimState.Loading;
            _loadAt = Time.realtimeSinceStartup;
            _readySince = -1f; _builtAt = -1f; _built = false;
            _t.Clear(); Last = null;
        }

        // ================= update =================
        private void Update()
        {
            if (!Active) return;
            try
            {
                switch (State)
                {
                    case BtcAimState.Loading: TickLoading(); break;
                    case BtcAimState.Countdown:
                        CountdownLeft -= Time.unscaledDeltaTime;
                        if (CountdownLeft <= 0f) StartRun();
                        break;
                    case BtcAimState.Run: TickRun(); break;
                    case BtcAimState.Over:
                        if (_gcAt > 0f && Time.realtimeSinceStartup >= _gcAt) { _gcAt = -1f; CollectIfNeeded("result"); }
                        break;
                }
                if (State != BtcAimState.Loading && State != BtcAimState.Idle)
                {
                    if (!NetworkServer.active) { Deactivate("session ended"); return; }
                    if (Input.GetKeyDown(KeyCode.R) && GUIUtility.keyboardControl == 0 && !ResourcePackManager.MenuIsOpen) Again();
                    if (Time.unscaledTime > _sweepAt) { _sweepAt = Time.unscaledTime + 3f; SweepStrays(); }
                }
            }
            catch (Exception e) { BtcLog.Error("Aim", "Update", e); }
        }

        private void LateUpdate()
        {
            if (!CameraLocked) return;
            try { if (_cs == null) _cs = FindObjectOfType<CameraScript>(); if (_cs != null) PoseCamera(_cs); } catch (Exception) { }
        }

        private void TickLoading()
        {
            if (!Ready())
            {
                _readySince = -1f;
                // the empty map gave no faction (sandbox needs spawn markers there): remember it, the next start uses the default map
                if (_onVoid && Time.realtimeSinceStartup - _loadAt > 20f && Architecte.commence && Architecte.physicsReady && (User.LocalUser == null || User.LocalUser.faction == null))
                {
                    BtcAimConfig.EmptyMap = false;
                    BtcLog.Warn("Aim", "empty map: no local faction after 20 s, switching to the default map");
                    BtcHud.Notice(BtcGameplay.T("The empty map does not work here: press Start again (the default map will be used)", "Пустая карта тут не работает: нажми Старт ещё раз (будет обычная карта)"), BtcReplayIcons.Target, BtcHud.Warn);
                    ExitToMenu();
                }
                return;
            }
            if (_readySince < 0f) _readySince = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _readySince < 0.5f) return;
            if (_builtAt >= 0f) { SettleStep(); return; }
            if (!FindFactions()) { Deactivate("no second faction"); return; }
            ClearStartUnits();
            _fogWas = LocalFactionVision.IsEnabled();
            if (_fogWas) LocalFactionVision.ToggleFogSystem(false);
            if (!BuildFloor()) { Deactivate("floor build failed"); return; }
            CameraLocked = true;
            _builtAt = Time.realtimeSinceStartup; _calmSince = -1f;
        }

        /// <summary>Short pause after the floor edit (navigation picks the slab up within a few frames): 0.5 s of calm frames, at most 3 s.</summary>
        private void SettleStep()
        {
            float now = Time.realtimeSinceStartup;
            bool calm = Time.unscaledDeltaTime < 0.03f;
            if (!calm) _calmSince = -1f; else if (_calmSince < 0f) _calmSince = now;
            if ((_calmSince >= 0f && now - _calmSince > 0.5f) || now - _builtAt > 3f)
            {
                BtcLog.Info("Aim", "floor settled after " + (now - _builtAt).ToString("0.0") + " s");
                _builtAt = -1f;
                StartCountdown();
            }
        }

        private static bool Ready()
        {
            try
            {
                if (!Architecte.commence || !Architecte.physicsReady || !VoxelEngine.Ready) return false;
                User u = User.LocalUser;
                return u != null && u.faction != null && (_onVoid || CrystalOf(u.faction) != null);
            }
            catch (Exception) { return false; }
        }

        private static LuaDriver CrystalOf(Faction f)
        {
            LuaStaticDriver sd;
            if (f != null && LuaDriverHQ.AllStatics.TryGetValue("fy_Crystal", out sd)) return sd.GetFirstOfFaction(f);
            return null;
        }

        private bool FindFactions()
        {
            _me = User.LocalUser.faction;
            _enemy = null;
            List<Faction> all = Faction.GetAllFactions();
            for (int i = 0; i < all.Count; i++)
            {
                Faction f = all[i];
                if (f == null || f == _me || f.HasOwner() || f.IsSpectator || f.isAI || f == Faction.DefaultNeutral) continue;
                _enemy = f; break;
            }
            if (_enemy == null) { BtcLog.Error("Aim", "no free faction for the targets"); return false; }
            try { _me.SetFactionRelation(_enemy, Affiliation.Relation.Enemy); _enemy.SetFactionRelation(_me, Affiliation.Relation.Enemy); }
            catch (Exception e) { BtcLog.Warn("Aim", "relation: " + e.Message); }
            return true;
        }

        private void ClearStartUnits()
        {
            int n = 0;
            Labor[] ls = FindObjectsOfType<Labor>();
            for (int i = 0; i < ls.Length; i++)
            {
                Labor l = ls[i];
                if (l == null) continue;
                if (_me.IsSame(l.gameObject) || _enemy.IsSame(l.gameObject)) { BtcDuel.Despawn(l.gameObject); n++; }
            }
            BtcLog.Info("Aim", "removed start units: " + n);
        }

        /// <summary>Every 3 s: units of both factions that are not targets (workers the crystal hands out) are removed.</summary>
        private void SweepStrays()
        {
            Labor[] ls = FindObjectsOfType<Labor>();
            int n = 0;
            for (int i = 0; i < ls.Length; i++)
            {
                Labor l = ls[i];
                if (l == null || IndexOf(l.gameObject) >= 0) continue;
                if (_me.IsSame(l.gameObject) || _enemy.IsSame(l.gameObject))
                {
                    if (n == 0 && _strayLog++ < 3) BtcLog.Warn("Aim", "stray " + l.gameObject.name + " mine=" + _me.IsSame(l.gameObject) + " pos=" + l.transform.position + " active=" + l.gameObject.activeInHierarchy + " prof=" + l.GetComponent<Profession>() + " root=" + (l.transform.parent == null));
                    BtcDuel.Despawn(l.gameObject); n++;
                }
            }
            if (n > 0) BtcLog.Warn("Aim", "stray units removed: " + n);
        }

        private int _strayLog;

        private int IndexOf(GameObject go)
        {
            for (int i = 0; i < _t.Count; i++) if (_t[i].Go == go) return i;
            return -1;
        }

        // ================= floor =================
        private static void Grid(int n, out int cols, out int rows) { cols = Mathf.CeilToInt(Mathf.Sqrt(n)); rows = Mathf.CeilToInt(n / (float)cols); }


        private static float Floor { get { return FloorTop + 1f; } }

        /// <summary>Where the slab goes: the nearest spot to the home crystal whose square plus a wide border is empty sky (over the void),
        /// inside the camera bounds of the map. None found = right over the crystal (the crystals are then moved far away).</summary>
        private bool PickSpot(Vector3 anchor, out int x0, out int z0)
        {
            x0 = Mathf.RoundToInt(anchor.x) - Side / 2; z0 = Mathf.RoundToInt(anchor.z) - Side / 2;
            try
            {
                CameraScript cs = FindObjectOfType<CameraScript>();
                if (cs == null) return false;
                Bounds b = cs.BoundsLimit;
                const int border = 40, stepC = 16;
                int minX = Mathf.CeilToInt(b.min.x) + border, maxX = Mathf.FloorToInt(b.max.x) - border - Side;
                int minZ = Mathf.CeilToInt(b.min.z) + border, maxZ = Mathf.FloorToInt(b.max.z) - border - Side;
                if (maxX <= minX || maxZ <= minZ) return false;
                float best = float.MaxValue; bool found = false;
                for (int x = minX; x <= maxX; x += stepC)
                    for (int z = minZ; z <= maxZ; z += stepC)
                    {
                        float d = new Vector2(x + Side * 0.5f - anchor.x, z + Side * 0.5f - anchor.z).sqrMagnitude;
                        if (d >= best || !EmptySquare(x - border, z - border, Side + border * 2, 4)) continue;
                        best = d; x0 = x; z0 = z; found = true;
                    }
                BtcLog.Info("Aim", "spot search bounds=" + b.min.x.ToString("0") + "," + b.min.z.ToString("0") + ".." + b.max.x.ToString("0") + "," + b.max.z.ToString("0") +
                    (found ? " void spot at " + x0 + "," + z0 + " dist=" + Mathf.Sqrt(best).ToString("0") : " no void spot"));
                return found;
            }
            catch (Exception e) { BtcLog.Warn("Aim", "spot search: " + e.Message); return false; }
        }

        /// <summary>No home crystal (empty map): the middle of the camera bounds.</summary>
        private static Vector3 WorldCentre()
        {
            try { CameraScript cs = FindObjectOfType<CameraScript>(); if (cs != null && cs.BoundsLimit.size.x > 1f) return cs.BoundsLimit.center; } catch (Exception) { }
            return new Vector3(512f, 0f, 512f);
        }

        private static bool EmptySquare(int x0, int z0, int side, int step)
        {
            for (int x = x0; x < x0 + side; x += step)
                for (int z = z0; z < z0 + side; z += step)
                    if (!VoxelEngine.IsEmptyColumn(x, z)) return false;
            return true;
        }

        /// <summary>A 1-voxel slab at FloorTop. Columns get bottom = top, so nothing hangs under the floor; checked after the apply and
        /// fixed if the engine treats bottom as exclusive. Built once per session.</summary>
        private bool BuildFloor()
        {
            try
            {
                if (!VoxelEngine.Ready) return false;
                LuaDriver home = CrystalOf(_me);
                Vector3 anchor = home != null ? home.transform.position : WorldCentre();
                int x0, z0;
                bool sky = PickSpot(anchor, out x0, out z0);
                if (!Slab(x0, z0) && sky)
                {
                    BtcLog.Warn("Aim", "void spot did not take the floor: back over the crystal");
                    sky = false;
                    x0 = Mathf.RoundToInt(anchor.x) - Side / 2; z0 = Mathf.RoundToInt(anchor.z) - Side / 2;
                    if (!Slab(x0, z0)) return false;
                }
                if (!sky) MoveCrystalsAway();
                _center = new Vector3(x0 + Side * 0.5f, Floor, z0 + Side * 0.5f);
                _built = true;
                return true;
            }
            catch (Exception e) { BtcLog.Error("Aim", "floor build failed", e); return false; }
        }

        private static bool Slab(int x0, int z0)
        {
            VoxelEngine.BeginChanges();
            for (int x = x0; x < x0 + Side; x++)
                for (int z = z0; z < z0 + Side; z++)
                {
                    VoxelEngine.SetColumnTopVoxel(x, z, FloorTop);
                    VoxelEngine.SetColumnBottom(x, z, FloorTop);
                    VoxelEngine.SetColumnType(x, z, ((x + z) & 1) == 0 ? VoxelTerrainType.Stone : VoxelTerrainType.Pavement);
                    VoxelEngine.SetColumnCrustVolume(x, z, 1f);
                }
            VoxelEngine.ApplyChanges(false);
            int cx = x0 + Side / 2, cz = z0 + Side / 2, b, t;
            VoxelEngine.GetColumnData(cx, cz, out b, out t);
            if (b >= t)
            {
                VoxelEngine.BeginChanges();
                for (int x = x0; x < x0 + Side; x++) for (int z = z0; z < z0 + Side; z++) VoxelEngine.SetColumnBottom(x, z, FloorTop - 1);
                VoxelEngine.ApplyChanges(false);
                VoxelEngine.GetColumnData(cx, cz, out b, out t);
            }
            bool ok = b < t && t >= FloorTop;
            if (ok)
                for (int x = x0; x < x0 + Side; x++)
                    for (int z = z0; z < z0 + Side; z++)
                        for (int dy = 0; dy <= 1; dy++) Fire(x, FloorTop + dy, z);
            BtcLog.Info("Aim", "floor side=" + Side + " x0=" + x0 + " z0=" + z0 + " col(b,t)=" + b + "," + t + (ok ? "" : " FAILED"));
            return ok;
        }

        private static void Fire(int x, int y, int z)
        {
            XYZ p = new XYZ(x, y, z);
            GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, p);
            GameSignals.Invoke<XYZ>(GameSignals.TerrainChanged, p);
            Architecte.Changement(p);
        }

        /// <summary>Fallback when the floor hangs over the island: the crystals of both factions go 1000 blocks away so they are not seen under it.</summary>
        private void MoveCrystalsAway()
        {
            Faction[] fs = { _me, _enemy };
            for (int i = 0; i < fs.Length; i++)
            {
                LuaDriver c = CrystalOf(fs[i]);
                if (c == null) continue;
                Vector3 p = c.transform.position;
                c.transform.position = new Vector3(p.x + 1000f, p.y, p.z + 1000f);
                BtcLog.Info("Aim", "crystal of " + fs[i].name + " moved away");
            }
        }

        // ================= camera =================
        internal static void PoseCamera(CameraScript cs)
        {
            BtcAim r = Run;
            if (r == null || !r._built) return;
            float h = BtcAimConfig.Height, pitch = BtcAimConfig.Pitch, yaw = BtcAimConfig.Yaw;
            if (r.State == BtcAimState.Run || r.State == BtcAimState.Countdown) { h = r._cfg.Height; pitch = r._cfg.Pitch; yaw = r._cfg.Yaw; }
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 fwd = rot * Vector3.forward;
            Vector3 pos = r._center - fwd * h;
            CameraScript.Target = r._center;
            cs.Distance = h; cs.AngleOfView = pitch; cs.CamOrientation = yaw;
            Transform t = cs.transform;
            t.position = pos; t.rotation = rot;
            Camera c = Camera.main;
            if (c == null) return;
            if (c.transform != t) { c.transform.position = pos; c.transform.rotation = rot; }
            if (r._farWas < 0f) r._farWas = c.farClipPlane;
            // far plane right behind the farthest floor corner: everything under the slab (the island, its crystal) is clipped at any distance
            float far = 0f, half = Side * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = r._center + new Vector3((i & 1) == 0 ? -half : half, 0f, (i & 2) == 0 ? -half : half);
                far = Mathf.Max(far, Vector3.Dot(corner - pos, fwd));
            }
            c.farClipPlane = far + 4f;
            c.nearClipPlane = Mathf.Clamp(h * 0.01f, 0.3f, 10f);
            Fov = c.fieldOfView;
        }

        private void RestoreCamera()
        {
            CameraLocked = false;
            try { Camera c = Camera.main; if (c != null && _farWas > 0f) { c.farClipPlane = _farWas; c.nearClipPlane = 0.3f; } } catch (Exception) { }
            _farWas = -1f;
        }

        // ================= flow =================
        internal void StartCountdown()
        {
            DespawnAll();
            _cfg = BtcAimConfig.Take();
            _packOff = _packDir = Vector3.zero; _packNext = 0f; _packRest = -1f;
            Hits = Misses = 0; Elapsed = 0f;
            Last = null;
            SpawnTargets();
            State = BtcAimState.Countdown;
            CountdownLeft = CountdownSec;
            _gcAt = -1f;
            CollectIfNeeded("countdown");
        }

        // ================= garbage collection =================
        // Unity 5.6 Mono cannot switch the GC off; a full collect costs ~0.3 s here. It runs only in the countdown (never in a round), and only
        // when the garbage since the last collect could make the next round trigger one: the round then starts with a large free heap.
        private long _memAfterGc = -1;
        private float _gcAt = -1f;
        private float _allocRate = 4f * 1048576f;      // bytes/s during rounds, measured (start value from the lab: ~3.7 MB/s)
        private long _memAtRun = -1;
        private float _runT0;

        /// <summary>where: "result" (stats screen, the main place: the round is over, nothing to click) or "countdown" (safety net).</summary>
        private void CollectIfNeeded(string where)
        {
            try
            {
                long mem = GC.GetTotalMemory(false);
                long grown = _memAfterGc < 0 ? long.MaxValue : mem - _memAfterGc;
                long next = (long)(_allocRate * (_cfg.Duration + 10f));          // what the next round will allocate, with room
                long budget = where == "result" ? 16L * 1048576L : Math.Max(48L * 1048576L, next);
                if (grown < budget) { BtcLog.Info("Aim", "gc skipped (" + where + "): +" + (grown / 1048576) + " MB since last (budget " + (budget / 1048576) + " MB)"); return; }
                float t0 = Time.realtimeSinceStartup;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                _memAfterGc = GC.GetTotalMemory(false);
                BtcLog.Info("Aim", "gc on " + where + " " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms, heap " + (mem / 1048576) + " -> " + (_memAfterGc / 1048576) + " MB");
            }
            catch (Exception e) { BtcLog.Warn("Aim", "gc: " + e.Message); }
        }

        /// <summary>Allocation rate of the round just played (heap growth / time; a GC inside the round makes it negative, then the old rate stays).</summary>
        private void MeasureAlloc()
        {
            try
            {
                if (_memAtRun < 0) return;
                float dt = Time.realtimeSinceStartup - _runT0;
                long d = GC.GetTotalMemory(false) - _memAtRun;
                if (dt > 3f && d > 0) _allocRate = Mathf.Lerp(_allocRate, d / dt, 0.5f);
                BtcLog.Info("Aim", "round alloc " + (d / 1024) + " KB in " + dt.ToString("0.0") + " s (rate " + (_allocRate / 1048576f).ToString("0.0") + " MB/s)");
            }
            catch (Exception) { }
        }

        private void SpawnTargets()
        {
            int n = _cfg.Targets, cols, rows; Grid(n, out cols, out rows);
            Quaternion face = Quaternion.Euler(0f, _cfg.Yaw + 180f, 0f);
            Vector3 right = Quaternion.Euler(0f, _cfg.Yaw, 0f) * Vector3.right, fwd = Quaternion.Euler(0f, _cfg.Yaw, 0f) * Vector3.forward;
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, rr = i / cols;
                int inRow = Mathf.Min(cols, n - rr * cols);
                float ox = (c - (inRow - 1) * 0.5f) * _cfg.Spacing, oz = ((rows - 1) * 0.5f - rr) * _cfg.Spacing;
                Vector3 p = _center + right * ox + fwd * oz;
                p.y = Floor + 0.08f;
                GameObject go = BtcDuel.SpawnUnit(KindOf(_cfg.Unit), _enemy, p, face);
                if (go == null) { BtcLog.Warn("Aim", "spawn failed"); continue; }
                Target t = new Target { Go = go, Tf = go.transform, Labor = go.GetComponent<Labor>(), Dr = go.GetComponent<DamageReceiver>(), Home = p, Dest = p };
                t.Dir = (i & 1) == 0 ? 1 : -1;
                _t.Add(t);
                if (_cfg.Unit == (int)BtcAimUnit.KnightShield) Defend(t);
                if (i == 0) MeasureWidth(go);
            }
            BtcLog.Info("Aim", "targets=" + _t.Count + " grid=" + cols + "x" + rows + " width=" + UnitWidth.ToString("0.00"));
        }

        private static BtcDuelUnit KindOf(int unit)
        {
            return unit == (int)BtcAimUnit.Worker ? BtcDuelUnit.Worker : unit <= (int)BtcAimUnit.Knight ? BtcDuelUnit.Knight : BtcDuelUnit.Halberdier;
        }

        private static void MeasureWidth(GameObject go)
        {
            try
            {
                Collider[] cs = go.GetComponentsInChildren<Collider>();
                bool any = false; Bounds b = new Bounds();
                for (int i = 0; i < cs.Length; i++)
                {
                    if (cs[i] == null || cs[i].gameObject.layer != UnitLayer) continue;   // the pick volume, as clicks use it
                    if (!any) { b = cs[i].bounds; any = true; } else b.Encapsulate(cs[i].bounds);
                }
                if (!any)
                    for (int i = 0; i < cs.Length; i++)
                    {
                        if (cs[i] == null || cs[i].isTrigger) continue;
                        if (!any) { b = cs[i].bounds; any = true; } else b.Encapsulate(cs[i].bounds);
                    }
                if (any) UnitWidth = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z), 0.4f, 2.5f);
            }
            catch (Exception) { }
        }

        private void StartRun()
        {
            State = BtcAimState.Run;
            _memAtRun = GC.GetTotalMemory(false); _runT0 = Time.realtimeSinceStartup;
            TimeLeft = _cfg.Duration;
            Last = new BtcAimScore.Result { Cfg = _cfg };
            float now = Time.time;
            for (int i = 0; i < _t.Count; i++) _t[i].NextMoveAt = now + (float)_rng.NextDouble() * 0.6f;
            NextRound();
        }

        /// <summary>New round: the previous wounded heal (their HP ring is removed at once, vanilla would keep it 1 s), new ones drop.</summary>
        private void NextRound()
        {
            int alive = 0;
            for (int i = 0; i < _t.Count; i++) if (_t[i].Go != null) alive++;
            for (int i = 0; i < _t.Count; i++) _t[i].Want = 1f;
            _wounded = 0;
            if (alive == 0) return;
            _pick.Clear();
            for (int i = 0; i < _t.Count; i++) if (_t[i].Go != null) _pick.Add(i);
            if (_cfg.Game == (int)BtcAimGame.Triage)
            {
                int k = Mathf.Min(alive, BtcAimConfig.WoundedCount(_cfg.Targets, _cfg.Wound));
                float step = _cfg.Step / 100f;
                // levels base + step * r, r in 0..k-1 (repeats allowed), at least one at the base; all between 10% and 90%
                float span = step * (k - 1);
                float lo = 0.10f, hi = Mathf.Max(lo, 0.90f - span);
                float bas = lo + (float)_rng.NextDouble() * (hi - lo);
                for (int j = 0; j < k; j++)
                {
                    int s = _rng.Next(j, _pick.Count); int tmp = _pick[j]; _pick[j] = _pick[s]; _pick[s] = tmp;
                    int r = j == 0 ? 0 : _rng.Next(0, k);
                    _t[_pick[j]].Want = Mathf.Clamp(bas + step * r, 0.05f, 0.95f);
                }
                _wounded = k;
            }
            else
            {
                int prev = -1;
                for (int i = 0; i < _t.Count; i++) if (_t[i].Dr != null && _t[i].Dr.CurrentHP < _t[i].Dr.MaxHP - 0.5f) prev = i;
                int pick;
                do pick = _pick[_rng.Next(_pick.Count)]; while (pick == prev && alive > 1);
                _t[pick].Want = BtcAimConfig.LowHp;
                _wounded = 1;
            }
            _minWant = 1f;
            for (int i = 0; i < _t.Count; i++) if (_t[i].Want < _minWant) _minWant = _t[i].Want;
            ApplyHp(true);
            _roundAt = Time.unscaledTime;
            // Fitts distance / width: to the nearest correct target
            Camera c = Camera.main;
            Vector2 m = Input.mousePosition;
            float bestD = float.MaxValue; int bi = -1;
            for (int i = 0; i < _t.Count; i++)
            {
                if (!IsCorrect(i)) continue;
                float d = (ToScreen(c, _t[i].Tf.position) - m).magnitude;
                if (d < bestD) { bestD = d; bi = i; }
            }
            _roundD = bi >= 0 ? bestD : 0f;
            _roundW = bi >= 0 ? ScreenWidth(c, _t[bi].Tf.position) : 20f;
        }

        private bool IsCorrect(int i) { return i >= 0 && i < _t.Count && _t[i].Go != null && _t[i].Want < 0.999f && _t[i].Want <= _minWant + 0.001f; }

        /// <summary>Holds every target at its wanted HP (no regen). dropRings: healed targets lose the HP ring right away.</summary>
        private void ApplyHp(bool dropRings)
        {
            SelectionCircleDispenser disp = dropRings ? SelectionCircleDispenser.Instance : null;
            for (int i = 0; i < _t.Count; i++)
            {
                Target t = _t[i];
                if (t.Go == null || t.Dr == null) continue;
                float want = Mathf.Max(1f, t.Dr.MaxHP * t.Want);
                if (t.Want >= 0.999f) want = t.Dr.MaxHP;
                bool healed = t.Dr.CurrentHP < want - 0.5f && t.Want >= 0.999f;
                if (Mathf.Abs(t.Dr.CurrentHP - want) > 0.5f) t.Dr.CurrentHP = want;
                if (healed && disp != null) { try { disp.Remove(t.Go, 0f); } catch (Exception) { } }
            }
        }

        private static Vector2 ToScreen(Camera c, Vector3 p)
        {
            if (c == null) return Vector2.zero;
            Vector3 s = c.WorldToScreenPoint(p + Vector3.up * 0.8f);
            return new Vector2(s.x, s.y);
        }

        private static float ScreenWidth(Camera c, Vector3 p)
        {
            if (c == null) return 20f;
            Vector3 mid = p + Vector3.up * 0.8f, r = c.transform.right * (UnitWidth * 0.5f);
            return (c.WorldToScreenPoint(mid + r) - c.WorldToScreenPoint(mid - r)).magnitude;
        }

        private void TickRun()
        {
            float dt = Time.unscaledDeltaTime;
            Elapsed += dt; TimeLeft -= dt;
            if (TimeLeft <= 0f) { Finish(); return; }
            ApplyHp(false);
            if (_cfg.Move != (int)BtcAimMove.Stand) MoveTick();
            if (Input.GetMouseButtonDown(1) && !BtcAimHud.MouseOverHud && !ResourcePackManager.MenuIsOpen) Click(Input.mousePosition);
#if DEV
            if (DevAutoMs > 0f && (Time.unscaledTime - _roundAt) * 1000f >= DevAutoMs)
            {
                for (int i = 0; i < _t.Count; i++) if (IsCorrect(i)) { Click(ToScreen(Camera.main, _t[i].Tf.position)); break; }
            }
#endif
        }

        private static readonly RaycastHit[] NoHits = new RaycastHit[0];
        /// <summary>Layer the vanilla pickers use for units (BlueprintPMPicker.unitLayer).</summary>
        private const int UnitLayer = 22;

        /// <summary>Every unit on the ray counts (a click into a pack hits the right unit when it is in the pack); with none on the ray, a correct
        /// target standing in the same block as the first unit hit still counts.</summary>
        private void Click(Vector2 at)
        {
            Camera c = Camera.main;
            if (c == null || Last == null) return;
            RaycastHit[] hits;
            // the vanilla unit picker: layer 22 with trigger colliders (the pick volume is larger than the body); body colliders as a fallback
            Ray ray = c.ScreenPointToRay(at);
            try { hits = Physics.RaycastAll(ray, 5000f, 1 << UnitLayer, QueryTriggerInteraction.Collide); } catch (Exception) { hits = NoHits; }
            if (hits.Length == 0) { try { hits = Physics.RaycastAll(ray, 5000f, -1, QueryTriggerInteraction.Ignore); } catch (Exception) { hits = NoHits; } }
            int first = -1; float firstD = float.MaxValue, wrongWounded = -1f;
            int good = -1;
            for (int i = 0; i < hits.Length; i++)
            {
                Labor l = hits[i].collider != null ? hits[i].collider.GetComponentInParent<Labor>() : null;
                if (l == null) continue;
                int ti = IndexOf(l.gameObject);
                if (ti < 0) continue;
                if (IsCorrect(ti)) { good = ti; break; }
                if (_t[ti].Want < 0.999f) wrongWounded = 1f;
                if (hits[i].distance < firstD) { firstD = hits[i].distance; first = ti; }
            }
            if (good < 0 && first >= 0)
            {
                Vector3 fp = _t[first].Tf.position;
                for (int i = 0; i < _t.Count; i++)
                {
                    if (!IsCorrect(i)) continue;
                    Vector3 p = _t[i].Tf.position;
                    if (new Vector2(p.x - fp.x, p.z - fp.z).sqrMagnitude < 0.75f * 0.75f && Mathf.Abs(p.y - fp.y) < 1f) { good = i; break; }
                }
            }
            if (good >= 0)
            {
                float mt = Time.unscaledTime - _roundAt;
                Vector2 sp = ToScreen(c, _t[good].Tf.position);
                float off = (sp - at).magnitude / Mathf.Max(2f, _roundW);
                float id = BtcAimScore.HitId(_roundD, _roundW, _cfg.Targets, BtcAimScore.MotionFactor(_cfg.Move, _cfg.Unit), _wounded, _cfg.Step);
                Last.Hits.Add(new BtcAimScore.Hit { Mt = mt, D = _roundD, W = _roundW, Off = off, Id = id });
                Hits++;
                BtcAimHud.Flash(true);
                BtcSfx.Click();
                NextRound();
                return;
            }
            if (wrongWounded > 0f) Last.MissPick++;
            else if (first >= 0) Last.MissWrong++;
            else Last.MissEmpty++;
            Misses++;
            BtcAimHud.Flash(false);
            BtcSfx.Miss();
        }

        // pack movement: one shared offset from the home grid, a new direction when the pack has (mostly) arrived
        private Vector3 _packOff;
        private float _packNext, _packRest = -1f;

        private void MoveTick()
        {
            if (_cfg.Move == (int)BtcAimMove.Pack) { PackTick(); return; }
            float now = Time.time;
            Vector3 right = Quaternion.Euler(0f, _cfg.Yaw, 0f) * Vector3.right;
            for (int i = 0; i < _t.Count; i++)
            {
                Target t = _t[i];
                if (t.Go == null || t.Labor == null) continue;
                Vector3 p = t.Tf.position;
                bool arrived = new Vector2(p.x - t.Dest.x, p.z - t.Dest.z).sqrMagnitude < 0.25f;
                if (!arrived && now < t.NextMoveAt) continue;             // still walking (re-issued after 4 s when blocked by a neighbour)
                if (arrived)
                {
                    if (t.RestUntil < 0f) t.RestUntil = now + 0.3f * (float)_rng.NextDouble();
                    if (now < t.RestUntil) continue;
                }
                t.RestUntil = -1f;
                Vector3 d;
                if (_cfg.Move == (int)BtcAimMove.Strafe) { t.Dir = -t.Dir; d = t.Home + right * (Amp * t.Dir * (0.6f + 0.4f * (float)_rng.NextDouble())); }
                else
                {
                    float a = (float)(_rng.NextDouble() * Math.PI * 2.0), rad = Amp * (0.4f + 0.6f * (float)_rng.NextDouble());
                    d = t.Home + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                }
                SendMove(t, d, now);
            }
        }

        private void PackTick()
        {
            float now = Time.time;
            int moving = 0, alive = 0;
            for (int i = 0; i < _t.Count; i++)
            {
                Target t = _t[i];
                if (t.Go == null || t.Labor == null) continue;
                alive++;
                Vector3 p = t.Tf.position;
                if (new Vector2(p.x - t.Dest.x, p.z - t.Dest.z).sqrMagnitude >= 0.5f) moving++;
            }
            if (alive == 0) return;
            bool arrived = moving <= alive / 4;                      // most of the pack is there (a straggler does not hold everyone)
            if (!arrived && now < _packNext) return;
            if (arrived)
            {
                if (_packRest < 0f) _packRest = now + 0.35f * (float)_rng.NextDouble();
                if (now < _packRest) return;
            }
            _packRest = -1f;
            // a new direction at least 60 degrees away from the way it came, kept inside the floor
            Vector3 off = _packOff;
            for (int tries = 0; tries < 8; tries++)
            {
                float a = (float)(_rng.NextDouble() * Math.PI * 2.0), len = 2f + (float)_rng.NextDouble() * (PackRoom - 2f);
                Vector3 step = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * len;
                Vector3 cand = _packOff + step;
                if (cand.magnitude > PackRoom) cand = cand.normalized * PackRoom;
                Vector3 dir = cand - _packOff;
                if (dir.sqrMagnitude < 2f) continue;
                if (_packDir.sqrMagnitude > 0.01f && Vector3.Angle(dir, _packDir) < 60f) continue;
                off = cand; break;
            }
            _packDir = off - _packOff;
            _packOff = off;
            if (_packLog++ < 3) BtcLog.Info("Aim", "pack move off=" + off.x.ToString("0.0") + "," + off.z.ToString("0.0") + " first defending=" + (_t.Count > 0 && _t[0].Labor != null && _t[0].Labor.Defending));
            _packNext = now + 4f;
            for (int i = 0; i < _t.Count; i++)
            {
                Target t = _t[i];
                if (t.Go == null || t.Labor == null) continue;
                SendMove(t, t.Home + _packOff, now);
            }
        }

        private Vector3 _packDir;
        private int _packLog;

        private void SendMove(Target t, Vector3 d, float now)
        {
            d.y = Floor;
            t.Dest = d;
            t.NextMoveAt = now + 4f;
            try
            {
                Order.Move.RunToward.Send(t.Labor, new SpaceLocation(d, true), Labor.DecisionSource.User);
                if (_cfg.Unit == (int)BtcAimUnit.KnightShield) Defend(t);    // a player order can drop the stance: keep the shield up
            }
            catch (Exception e) { BtcLog.Warn("Aim", "move: " + e.Message); t.NextMoveAt = now + 2f; }
        }

        /// <summary>Knight with the shield up = the defensive stance (walks, cannot run).</summary>
        private static void Defend(Target t)
        {
            try { if (!t.Labor.Defending) t.Labor.Autonomy = AutonomyStatus.FromId(AutonomyStatus.Ids.waitForOrderDefensive); }
            catch (Exception e) { BtcLog.Warn("Aim", "shield up: " + e.Message); }
        }

        private void Finish()
        {
            State = BtcAimState.Over;
            MeasureAlloc();
            if (Last == null) return;
            Last.Duration = _cfg.Duration;
            BtcAimScore.Compute(Last);
            bool record = Last.Clicks > 0;          // a run without a single click (away from the keyboard) is not a result
#if DEV
            if (DevAutoMs > 0f) record = false;     // lab auto clicker never reaches the player history
#endif
            if (record) BtcAimScore.Record(Last);
            _gcAt = Time.realtimeSinceStartup + 0.25f;   // the result card is drawn first, then the collect runs behind it
            for (int i = 0; i < _t.Count; i++) _t[i].Want = 1f;
            ApplyHp(true);
            BtcLog.Info("Aim", "run over rating=" + Last.Rating + " hits=" + Last.Hits.Count + " acc=" + Last.Acc.ToString("0.00") + " tp=" + Last.Tp.ToString("0.00") + " mt=" + Last.AvgMt.ToString("0.000"));
        }

        /// <summary>Restart with the current settings (R or the button): only the targets are respawned, the floor stays.</summary>
        internal void Again()
        {
            if (State == BtcAimState.Loading || State == BtcAimState.Idle) return;
            BtcAimConfig.Save();
            StartCountdown();
        }

        private void DespawnAll()
        {
            for (int i = 0; i < _t.Count; i++) BtcDuel.Despawn(_t[i].Go);
            _t.Clear();
        }

        internal void ExitToMenu()
        {
            Deactivate("exit button");
            try { Architecte.QuitToMainMenu(); } catch (Exception e) { BtcLog.Error("Aim", "quit", e); }
        }

        private void Deactivate(string why)
        {
            BtcLog.Info("Aim", "deactivate: " + why);
            Active = false;
            State = BtcAimState.Idle;
            RestoreCamera();
            try { if (_fogWas && !LocalFactionVision.IsEnabled()) LocalFactionVision.ToggleFogSystem(true); } catch (Exception) { }
            _fogWas = false;
            _t.Clear(); _built = false;
        }

        private void OnGUI()
        {
            if (!Active || State == BtcAimState.Idle) return;
            try { BtcAimHud.Draw(this); }
            catch (Exception e) { BtcLog.Error("Aim", "OnGUI", e); }
        }
    }
}
