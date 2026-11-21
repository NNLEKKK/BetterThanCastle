using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Replay v4 instant seek (playback only, works for old replays too).
    /// While packets are fed (live, seek or the first-open preprocess pass), a playback keyframe (PK) is captured every SlotMs of stream time:
    /// per spawned netId the OnSerialize(initialState) payload of all its NetworkBehaviours (exactly a spawn payload), its pose, plus the
    /// terrain diff blob (BtcTerrainUndo baseline) and nature blob (BtcReplayAnchor). A jump restores the nearest PK at or before the target:
    /// terrain/nature blobs when they differ, objects missing in the PK are destroyed (UNET ObjectDestroy), objects missing in the world are
    /// spawned from their recorded spawn message with the PK payload, changed objects are deserialized in place; then the records up to the
    /// target (< SlotMs) are fed in one frame. Any exception falls back to the legacy rewind (cfg replay.txt InstantSeek=0 disables it).
    /// </summary>
    internal static class BtcReplaySeek
    {
        internal const long SlotMs = 10000L;
        private const int MaxBytes = 512 * 1024 * 1024;

        private sealed class Obj
        {
            public uint Id;
            public Vector3 Pos;
            public Quaternion Rot;
            public bool Root;
            public bool Scene;
            public byte[] Payload;
            public int AnimState;
            public float AnimCarry;
            public bool HasAnim;
            public bool Active;
            public uint ParentId;
            // animator parameters (floats; ints and bools stored as floats): running/speed blends are only sent on change
            public float[] AnimParams;
        }

        private sealed class Pk
        {
            public int Slot;
            public long Ms;
            public int Pos;
            public Obj[] Objs;
            public byte[] Terrain;
            public byte[] Nature;
            // debloc batches arrive in chunks (msg 52) collected in UnetDeblocMessageHandler.IncomingdataStream: the partial batch at the PK
            public byte[] Debloc;
            // client-only visuals (BtcSeekVisuals: crystals)
            public byte[] Visuals;
            public uint Hash;
        }

        private static readonly Dictionary<int, Pk> Pks = new Dictionary<int, Pk>();
        private static readonly Dictionary<uint, byte[]> SpawnMsgs = new Dictionary<uint, byte[]>();
        private static byte[] _lastTerrain, _lastNature;
        private static long _bytes;
        private static bool _applying;
        private static bool _failed;
        private static int _state; // 0 idle, 1 preprocess running, 2 done
        private static float _origin, _ppT0;
        private static float _nextCheck;
        private static double _capMs;
        private static int _capN;
        private static bool _cfg, _enabled = true, _preprocess = true;

        internal static bool Preprocessing { get { return _state == 1; } }
        /// <summary>DEV: suspend jumps (legacy seek) without touching the config.</summary>
        internal static bool DevNoJump;
        internal static bool DevNoPreprocess;
        private static bool _jumpFeeding;

        /// <summary>Records are being fed faster than real time (seek, preprocess, the remainder of a jump).</summary>
        internal static bool FastFeed { get { return _state == 1 || _jumpFeeding || BtcReplay.Seeking; } }
        internal static int Count { get { return Pks.Count; } }
        internal static bool Enabled { get { EnsureCfg(); return _enabled && _preprocess; } }

        private static void EnsureCfg()
        {
            if (_cfg) return;
            _cfg = true;
            try
            {
                BtcPaths.Init();
                _enabled = BtcPaths.ReadKey("replay.txt", "InstantSeek", "1") != "0";
                _preprocess = BtcPaths.ReadKey("replay.txt", "SeekPreprocess", "1") != "0";
            }
            catch (Exception) { }
        }

        /// <summary>New replay opened (or closed): forget everything.</summary>
        internal static void Reset()
        {
            QuietEnd();
            Pks.Clear();
            SpawnMsgs.Clear();
            StatsMsgs.Clear();
            _statsLastMs = -1;
            BtcMatchStatsNet.ResetLegacy();
            _lastTerrain = null;
            _lastNature = null;
            _bytes = 0;
            _failed = false;
            _state = 0;
            _capMs = 0;
            _capN = 0;
            _cfg = false;
        }

        // ---------------- recording of the stream (spawn messages) ----------------

        // recorded stats messages (msg 101) with stream ms: after a jump the stats store is rebuilt from them (the jump only feeds the last <10 s)
        private struct StatsRec { internal long Ms; internal byte[] Raw; }
        private static readonly List<StatsRec> StatsMsgs = new List<StatsRec>();
        private static long _statsLastMs = -1;

        internal static void OnStatsMsg(byte[] msg, int len, long ms)
        {
            if (_applying || ms <= _statsLastMs) return; // monotonic append: re-fed records after a jump/rewind are already stored
            byte[] copy = new byte[len];
            Buffer.BlockCopy(msg, 0, copy, 0, len);
            _statsLastMs = ms;
            StatsMsgs.Add(new StatsRec { Ms = ms, Raw = copy });
        }

        /// <summary>Rebuild the replay stats store as of stream time ms (everything recorded before it).</summary>
        private static void RestoreStats(long ms)
        {
            if (StatsMsgs.Count == 0) return;
            BtcMatchStats.OnReplayStart();
            int n = 0;
            for (int i = 0; i < StatsMsgs.Count && StatsMsgs[i].Ms <= ms; i++) { BtcMatchStatsNet.ApplyRaw(StatsMsgs[i].Raw, StatsMsgs[i].Raw.Length); n++; }
            BtcLog.Info("ReplaySeek", "stats restored: " + n + "/" + StatsMsgs.Count + " msgs up to " + (ms / 1000f).ToString("0.0") + " s");
        }

        internal static void OnSpawnMsg(byte[] msg, int len)
        {
            if (_applying || len <= 5) return;
            try
            {
                NetworkReader r = new NetworkReader(msg);
                r.ReadUInt16();
                r.ReadUInt16();
                uint id = r.ReadNetworkId().Value;
                byte[] copy = new byte[len];
                Buffer.BlockCopy(msg, 0, copy, 0, len);
                SpawnMsgs[id] = copy;
            }
            catch (Exception e) { LogOnce("spawn msg parse: " + e.Message); }
        }

        // ---------------- capture ----------------

        internal static void AfterRecord(int pos, long ms)
        {
            if (_applying || _failed) return;
            EnsureCfg();
            if (!_enabled || !BtcReplay.FeedGo || BtcReplay.FeedRewinding || BtcReplayAnchor.Gating) return;
            int slot = (int)(ms / SlotMs);
            if (Pks.ContainsKey(slot) || _bytes > MaxBytes) return;
            try { // fast feeds queue deblocs without the batcher step running: apply them first or the PK misses the digs
                FlushVoxelQueue();
                // trees whose cut was skipped by the fast feed are only removed at the end of a seek: settle them before the PK
                if (BtcTreeCutSkipPatch.Skipped.Count > 0) BtcReplayAnchor.AfterSeekForward();
                Capture(slot, pos, ms); }
            catch (Exception e)
            {
                _failed = true;
                BtcLog.Error("ReplaySeek", "capture failed, instant seek off for this replay: " + e);
            }
        }

        private static readonly List<Obj> _tmp = new List<Obj>(1024);
        private static readonly List<uint> _ids = new List<uint>(1024);

        private static void Capture(int slot, int pos, long ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            _tmp.Clear();
            NetworkWriter w = new NetworkWriter();
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects)
            {
                NetworkIdentity id = kv.Value;
                if (id == null || IsGhost(kv.Key.Value, id)) continue;
                Obj o = new Obj();
                o.Id = kv.Key.Value;
                Transform t = id.transform;
                o.Pos = t.position;
                o.Rot = t.rotation;
                // "root" = no networked ancestor: dynamic objects live under scene containers (ObjetsDynamiques) and need their pose too
                o.Root = ParentOf(id.gameObject) == 0u;
                o.Scene = !id.sceneId.IsEmpty();
                // inactive but registered objects are real state too (stored/hidden items): keep them and their active flag
                o.Active = id.gameObject.activeSelf;
                UNetAnimator an = o.Active ? id.GetComponent<UNetAnimator>() : null;
                if (an != null && an.animator != null && an.animator.isActiveAndEnabled)
                {
                    try
                    {
                        // client copies of the spawn animator fields are stale (host only refreshes them): put the live pose in, so a
                        // respawn from this payload plays the right state (OnStartPureClient) and an in-place restore can Play() it
                        // fast feeds (preprocess) set flags/params without animator frames: the state machine still sits in the old state (a unit that
                        // already stopped is saved as "running" and Play() would pin it there). Evaluate the transitions for the params first.
                        an.animator.Update(0f);
                        o.AnimState = an.animator.IsInTransition(0) ? an.animator.GetNextAnimatorStateInfo(0).fullPathHash : an.animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
                        o.AnimCarry = an.animator.GetFloat("CarryMode");
                        o.HasAnim = true;
                        an.NetworkserverAnimatorStateOnSpawn = o.AnimState;
                        an.NetworkserverAnimatorCarryModeOnSpawn = o.AnimCarry;
                        o.AnimParams = ReadParams(an.animator);
                    }
                    catch (Exception) { }
                }
                o.ParentId = ParentOf(id.gameObject);
                o.Payload = Serialize(id, w);
                _tmp.Add(o);
            }
            _tmp.Sort((a, b) => a.Id.CompareTo(b.Id));
            Pk pk = new Pk();
            pk.Slot = slot;
            pk.Ms = ms;
            pk.Pos = pos;
            pk.Objs = _tmp.ToArray();
            pk.Hash = HashObjs(pk.Objs);
            long bytes = 0;
            for (int i = 0; i < pk.Objs.Length; i++) bytes += pk.Objs[i].Payload.Length + 48;
            byte[] terr = BtcReplayAnchor.TerrainBlob();
            pk.Terrain = Same(terr, _lastTerrain) ? _lastTerrain : terr;
            if (pk.Terrain != _lastTerrain && terr != null) bytes += terr.Length;
            _lastTerrain = pk.Terrain;
            byte[] nat = BtcReplayAnchor.NatureBlob();
            pk.Nature = Same(nat, _lastNature) ? _lastNature : nat;
            if (pk.Nature != _lastNature && nat != null) bytes += nat.Length;
            _lastNature = pk.Nature;
            pk.Debloc = DeblocState();
            pk.Visuals = BtcSeekVisuals.Snap();
            _bytes += bytes;
            Pks[slot] = pk;
            double el = sw.Elapsed.TotalMilliseconds;
            _capMs += el;
            _capN++;
            if (_capN <= 3 || _capN % 30 == 0)
            {
                BtcLog.Info("ReplaySeek", "pk slot " + slot + " at " + (ms / 1000f).ToString("0.0") + " s: objs " + pk.Objs.Length + ", " + bytes + " B, "
                    + el.ToString("0.0") + " ms (total " + Pks.Count + " pks, " + (_bytes / 1024) + " KB, avg " + (_capMs / _capN).ToString("0.0") + " ms)");
            }
        }

        private static byte[] Serialize(NetworkIdentity id, NetworkWriter w)
        {
            w.SeekZero();
            NetworkBehaviour[] nbs = id.GetComponents<NetworkBehaviour>();
            for (int i = 0; i < nbs.Length; i++) nbs[i].OnSerialize(w, true);
            return w.ToArray();
        }

        private static void Deserialize(NetworkIdentity id, byte[] payload)
        {
            NetworkReader r = new NetworkReader(payload);
            NetworkBehaviour[] nbs = id.GetComponents<NetworkBehaviour>();
            for (int i = 0; i < nbs.Length; i++) nbs[i].OnDeserialize(r, true);
        }

        // ---------------- jump ----------------

        /// <summary>SeekTo entry: true when the jump was handled here.</summary>
        internal static bool TryJump(float target)
        {
            EnsureCfg();
            if (!_enabled || _failed || _state == 1 || BtcReplayAnchor.Busy || DevNoJump) return false;
            long tms = (long)(target * 1000f);
            long cur = BtcReplay.FeedMs;
            // short forward hop: plain feed is already cheap and keeps everything continuous
            if (tms >= cur && tms - cur < SlotMs && !BtcReplay.FeedEnded) return false;
            Pk pk = Best(tms);
            if (pk == null) return false;
            // forward jump whose best PK is behind the current position: feeding forward from here is cheaper
            if (tms >= cur && pk.Ms <= cur) return false;
            return Jump(pk, tms, true);
        }

        /// <summary>Target is before the first keyframe (start of the replay): restore the earliest keyframe instead of the old scene rewind.</summary>
        internal static bool TryJumpEarliest(float target)
        {
            EnsureCfg();
            if (!_enabled || _failed || _state == 1 || BtcReplayAnchor.Busy) return false;
            Pk first = null;
            foreach (KeyValuePair<int, Pk> kv in Pks) if (first == null || kv.Value.Ms < first.Ms) first = kv.Value;
            if (first == null) return false;
            return Jump(first, first.Ms, true);
        }

        /// <summary>DEV self-check: restore PK slot (exact, no feed) and compare, then feed to the next PK position and compare again.</summary>
        internal static string VerifyPair(int slot)
        {
            Pk a, b;
            if (!Pks.TryGetValue(slot, out a)) return "no pk " + slot;
            if (!Jump(a, a.Ms, false)) return "jump failed";
            string r1 = Verify();
            if (!Pks.TryGetValue(slot + 1, out b)) return "restore: " + r1;
            BtcReplay.FeedUntilPos(b.Pos);
            string r2 = Verify();
            return "restore: " + r1 + " | fed to next: " + r2;
        }

        /// <summary>DEV: move the world to PK slot exactly: linear = feed forward from here (reference), else jump.</summary>
        internal static string MoveTo(int slot, bool linear)
        {
            Pk p;
            if (!Pks.TryGetValue(slot, out p)) return "no pk " + slot;
            if (linear)
            {
                if (BtcReplay.FeedPos > p.Pos) return "linear needs a later slot (pos " + BtcReplay.FeedPos + " > " + p.Pos + ")";
                int n = BtcReplay.FeedUntilPos(p.Pos);
                return "linear fed " + n + " records to slot " + slot;
            }
            return Jump(p, p.Ms, false) ? "jumped to slot " + slot : "jump failed";
        }

        /// <summary>DEV: PK slot active flags vs the live world (call when the live world is at that slot, e.g. after a linear move).</summary>
        internal static string CompareActive(int slot)
        {
            Pk p;
            if (!Pks.TryGetValue(slot, out p)) return "no pk " + slot;
            Dictionary<string, int> d = new Dictionary<string, int>();
            int missingLive = 0;
            for (int i = 0; i < p.Objs.Length; i++)
            {
                Obj o = p.Objs[i];
                GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(o.Id));
                if (go == null) { missingLive++; continue; }
                if (go.activeSelf == o.Active) continue;
                NetworkIdentity lni = go.GetComponent<NetworkIdentity>();
                bool rel = false;
                try { rel = Brix.Game.Components.GameComponentExtender.IsNullOrReleased(go); } catch (Exception) { }
                string k = Kind(go.name) + (o.Active ? " pkOn/liveOff" : " pkOff/liveOn") + (rel ? " released" : "") + (lni != null && lni.netId.Value != o.Id ? " reused" : "")
                    + (go.GetComponent<Brix.Components.IBrixComponent>() == null ? " noBrix" : "");
                int n; d.TryGetValue(k, out n); d[k] = n + 1;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, int> kv in d) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append("; ");
            return "slot " + slot + " pk objs " + p.Objs.Length + ", not live " + missingLive + ", active mismatches: " + (sb.Length > 0 ? sb.ToString() : "none");
        }

        /// <summary>DEV: is voxel p dug/added in every PK terrain blob, and live.</summary>
        internal static string DevLayerProbe(Brix.Engine.XYZ p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            List<int> slots = new List<int>(Pks.Keys);
            slots.Sort();
            foreach (int s in slots)
            {
                byte[] c, l;
                sb.Append(s).Append(':').Append(BtcReplayAnchor.SplitLayerBlob(Pks[s].Terrain, out c, out l) ? BtcTerrainLayers.Has(l, p) : "old").Append(' ');
            }
            return p + " " + sb + "live " + BtcTerrainLayers.Has(BtcTerrainLayers.Snap(), p);
        }

        /// <summary>DEV: pose of one netId in every PK (root flag, parent) and live.</summary>
        internal static string DevPkPos(uint id)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            List<int> slots = new List<int>(Pks.Keys);
            slots.Sort();
            foreach (int s in slots)
                foreach (Obj o in Pks[s].Objs)
                    if (o.Id == id) { sb.Append(s).Append(':').Append(o.Pos.ToString("F1")).Append(o.Root ? "" : "c").Append(o.ParentId != 0 ? "p" + o.ParentId : "").Append(o.Active ? "" : "off").Append(' '); break; }
            GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
            string comps = "";
            if (go != null) foreach (NetworkBehaviour nb in go.GetComponents<NetworkBehaviour>()) comps += nb.GetType().Name + ",";
            return sb + " live " + (go != null ? go.transform.position.ToString("F1") + " nb " + comps : "-");
        }

        internal static int SlotCount { get { int m = -1; foreach (int k in Pks.Keys) if (k > m) m = k; return m + 1; } }

        private static Pk Best(long tms)
        {
            Pk best = null;
            for (int s = (int)(tms / SlotMs); s >= 0; s--)
            {
                Pk p;
                if (Pks.TryGetValue(s, out p) && p.Ms <= tms) { best = p; break; }
            }
            return best;
        }

        private static readonly MethodInfo StateChanged = typeof(UNetAnimator).GetMethod("stateChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static bool Jump(Pk pk, long tms, bool feed)
        {
            Stopwatch sw = Stopwatch.StartNew();
            float from = BtcReplay.FeedMs / 1000f;
            int visuals = 0;
            int adopted = 0;
            int destroyed = 0, spawned = 0, inPlace = 0, same = 0, missing = 0, reparented = 0, hooks = 0, terrChanged = 0, ghosts = 0;
            double tWorld = 0, tObj = 0, tFeed = 0, tTerrSnap = 0, tTerrApply = 0, tNatSnap = 0;
            _why.Clear();
            _applying = true;
            try
            {
                // terrain + nature
                byte[] terr = BtcReplayAnchor.TerrainBlob();
                tTerrSnap = sw.Elapsed.TotalMilliseconds;
                if (pk.Terrain != null && !Same(terr, pk.Terrain))
                {
                    try { terrChanged = (terr != null) ? BtcReplayAnchor.ApplyTerrainKey(terr, pk.Terrain) : -1; }
                    catch (Exception e) { LogOnce("terrain delta: " + e.Message); terrChanged = -1; }
                    if (terrChanged < 0) BtcReplayAnchor.ApplyTerrainKeyFull(pk.Terrain);
                    // no convergence passes: each costs ~100-300 ms (ApplyChanges) and the few voxels left after one delta were never fixed by them
                    for (int pass = 0; pass < 0; pass++)
                    {
                        byte[] again = BtcReplayAnchor.TerrainBlob();
                        if (again == null || Same(again, pk.Terrain)) break;
                        try { terrChanged += BtcReplayAnchor.ApplyTerrainKey(again, pk.Terrain); }
                        catch (Exception e) { LogOnce("terrain delta pass: " + e.Message); break; }
                    }
                }
                tTerrApply = sw.Elapsed.TotalMilliseconds - tTerrSnap;
                double tn0 = sw.Elapsed.TotalMilliseconds;
                byte[] nat = BtcReplayAnchor.NatureBlob();
                tNatSnap = sw.Elapsed.TotalMilliseconds - tn0;
                if (pk.Nature != null && !Same(nat, pk.Nature)) BtcReplayAnchor.ApplyNatureBlob(pk.Nature);
                tWorld = sw.Elapsed.TotalMilliseconds;

                Dictionary<uint, Obj> want = new Dictionary<uint, Obj>(pk.Objs.Length);
                for (int i = 0; i < pk.Objs.Length; i++) want[pk.Objs[i].Id] = pk.Objs[i];
                NetworkWriter w = new NetworkWriter();

                // 1. destroy what the PK does not have; children the PK keeps are detached first (their tracker hook re-attaches them below)
                _ids.Clear();
                _ghosts.Clear();
                _keepOff.Clear();
                foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects)
                {
                    if (kv.Value == null) { _ghosts.Add(kv.Key); continue; } // key kept with a destroyed identity: UNET then registers the respawn nowhere
                    if (IsGhost(kv.Key.Value, kv.Value))
                    {
                        // a pool-released entry UNET still lists; its GameObject may already serve another object: never destroy it.
                        // If the keyframe needs a real object under this netId, drop the stale entry so it is spawned fresh below.
                        if (want.ContainsKey(kv.Key.Value)) _ghosts.Add(kv.Key);
                        continue;
                    }
                    if (!want.ContainsKey(kv.Key.Value)) _ids.Add(kv.Key.Value);
                }
                for (int i = 0; i < _ghosts.Count; i++) ClientScene.objects.Remove(_ghosts[i]);
                ghosts = _ghosts.Count;
                for (int i = 0; i < _ids.Count; i++)
                {
                    if (Kill(_ids[i], want)) destroyed++;
                }

                // objects are visited parents first, so a spawn or a tracker hook always finds its parent
                List<Obj> order = ParentsFirst(pk.Objs, want);

                // 2. spawn what the world does not have now. An object UNET dropped from its list during the previous feed may still be in
                // the scene, alive: put it back under its netId instead of spawning a copy (user: clone units piling up on +10/-10 spam)
                Dictionary<uint, NetworkIdentity> sceneNis = null;
                int reRegistered = 0;
                Dictionary<uint, NetworkIdentity> loose = new Dictionary<uint, NetworkIdentity>();
                HashSet<GameObject> listed = new HashSet<GameObject>();
                foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects) if (kv.Value != null) listed.Add(kv.Value.gameObject);
                foreach (NetworkIdentity x in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
                {
                    if (x.netId.IsEmpty() || listed.Contains(x.gameObject) || loose.ContainsKey(x.netId.Value)) continue;
                    bool rel = false;
                    try { rel = Brix.Game.Components.GameComponentExtender.IsNullOrReleased(x.gameObject); } catch (Exception) { }
                    if (!rel) loose[x.netId.Value] = x;
                }
                _lastPk.Clear();
                for (int i = 0; i < order.Count; i++)
                {
                    Obj o = order[i];
                    _lastPk.Add(o.Id);
                    if (ClientScene.FindLocalObject(new NetworkInstanceId(o.Id)) != null) continue;
                    NetworkIdentity lost;
                    if (loose.TryGetValue(o.Id, out lost) && lost != null)
                    {
                        ClientScene.objects[lost.netId] = lost;
                        loose.Remove(o.Id);
                        adopted++;
                        continue;
                    }
                    byte[] src;
                    if (!SpawnMsgs.TryGetValue(o.Id, out src)) { missing++; continue; }
                    byte[] msg = RewriteSpawn(src, o);
                    if (msg != null && BtcReplay.FeedRaw(msg)) { spawned++; if (ClientScene.FindLocalObject(new NetworkInstanceId(o.Id)) == null) RegisterSpawned(o.Id, ref sceneNis, ref reRegistered); } else missing++;
                }

                // 3. in place: deserialize changed objects and run their SyncVar hooks like a normal update (a jump = one big delta);
                // objects whose parent still differs get their hooks re-fired; then active flags and root poses
                for (int i = 0; i < order.Count; i++)
                {
                    Obj o = order[i];
                    GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(o.Id));
                    if (go == null) continue;
                    NetworkIdentity ni = go.GetComponent<NetworkIdentity>();
                    if (ni == null) continue;
                    byte[] now = Serialize(ni, w);
                    if (Same(now, o.Payload)) { same++; }
                    else
                    {
                        hooks += DeserializeWithHooks(ni, o.Payload);
                        inPlace++;
                    }
                    if (ParentOf(go) != o.ParentId)
                    {
                        ForceHooks(ni);
                        reparented++;
                        if (ParentOf(go) != o.ParentId) CountWhy("parent " + Kind(go.name));
                    }
                    if (o.HasAnim) RestoreAnim(ni, o);
                    if (go.activeSelf != o.Active && !o.Scene) go.SetActive(o.Active);
                    if (!o.Active && !o.Scene) _keepOff.Add(go);
                    if (o.Root && ParentOf(go) == 0u)
                    {
                        go.transform.position = o.Pos;
                        go.transform.rotation = o.Rot;
                    }
                }
                visuals = BtcSeekVisuals.Apply(pk.Visuals);
                tObj = sw.Elapsed.TotalMilliseconds - tWorld;
            }
            catch (Exception e)
            {
                _applying = false;
                _failed = true;
                BtcLog.Error("ReplaySeek", "jump to " + (tms / 1000f).ToString("0.0") + " s failed, using the legacy rewind: " + e);
                return false;
            }
            _applying = false;

            // a partial debloc batch left from the old position would corrupt the next one (its digs never show)
            SetDeblocState(pk.Debloc);
            // 4. feed the remainder (< SlotMs of records)
            RestoreStats(pk.Ms);
            BtcReplay.FeedRestore(pk.Pos, pk.Ms);
            double t0 = sw.Elapsed.TotalMilliseconds;
            _jumpFeeding = true;
            int fed;
            try { fed = feed ? BtcReplay.FeedUntil(tms) : 0; }
            finally { _jumpFeeding = false; }
            tFeed = sw.Elapsed.TotalMilliseconds - t0;
            FlushVoxelQueue();
            BtcSeekVisuals.RebuildRecepteurs();
            _pkJumpFrame = Time.frameCount; // the keyframe just applied says which units exist now
            ScheduleSweep();
            int stations = BtcSeekVisuals.RefreshStations();
            BtcReplay.AfterJump();
            _keepOffFrames = 20;
            _keepOffUntilMs = tms + 300;
            _keepOffWhy.Clear();
            BtcLog.Info("ReplaySeek", "jump " + from.ToString("0.0") + " -> " + (tms / 1000f).ToString("0.0") + " s via pk " + pk.Slot + " (" + (pk.Ms / 1000f).ToString("0.0")
                + " s): destroyed " + destroyed + ", stale entries dropped " + ghosts + ", re-listed " + adopted + ", spawned " + spawned + ", in place " + inPlace + " (hooks " + hooks + ", reparented " + reparented + ")" + ", same " + same + ", missing " + missing + ", fed " + fed
                + " records, visuals " + visuals + ", stations " + stations + ", " + BtcSeekVisuals.LastRecepteurs + " records; world " + tWorld.ToString("0") + " ms (terrain snap " + tTerrSnap.ToString("0") + " apply " + tTerrApply.ToString("0") + " changed " + terrChanged + " [" + BtcReplayAnchor.LastDeltaTimes + "; " + BtcReplayAnchor.LastCreateInfo + "], nature snap " + tNatSnap.ToString("0")
                + "), objects " + tObj.ToString("0") + " ms, feed " + tFeed.ToString("0") + " ms, total " + sw.ElapsedMilliseconds + " ms" + (_why.Count > 0 ? "; still off: " + Why() : ""));
            return true;
        }

        private static readonly FieldInfo DeblocIn = typeof(Brix.Network.UnetDeblocMessageHandler).GetField("IncomingdataStream", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static System.IO.Stream DeblocStream()
        {
            Brix.Network.UNetManager m = Brix.Network.UNetManager.instance;
            if (m == null || m.DeblocMessageHandler == null || DeblocIn == null) return null;
            return DeblocIn.GetValue(m.DeblocMessageHandler) as System.IO.Stream;
        }

        private static byte[] DeblocState()
        {
            try
            {
                System.IO.MemoryStream s = DeblocStream() as System.IO.MemoryStream;
                if (s == null || s.Length == 0) return null;
                BtcLog.Info("ReplaySeek", "pk at a partial debloc batch (" + s.Length + " B)");
                return s.ToArray();
            }
            catch (Exception e) { LogOnce("debloc state: " + e.Message); return null; }
        }

        private static void SetDeblocState(byte[] b)
        {
            try
            {
                System.IO.Stream s = DeblocStream();
                if (s == null) return;
                if (s.Length > 0) LogOnce("jump dropped a partial debloc batch (" + s.Length + " B)");
                s.SetLength(0);
                s.Position = 0;
                if (b != null) s.Write(b, 0, b.Length);
            }
            catch (Exception e) { LogOnce("debloc set: " + e.Message); }
        }

        private static readonly MethodInfo BatcherFixedUpdate = typeof(Brix.Game.Batchers.VoxelRequestBatcher).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>Fed debloc/terrain requests wait in VoxelRequestBatcher until its FixedUpdate; a jump while paused (timeScale 0)
        /// would show the old terrain until play resumes. Run that step once now.</summary>
        internal static void FlushVoxelQueue()
        {
            try
            {
                Brix.Game.Batchers.VoxelRequestBatcher b = Brix.Game.Batchers.VoxelRequestBatcher.Instance;
                if (b != null && BatcherFixedUpdate != null) BatcherFixedUpdate.Invoke(b, null);
            }
            catch (Exception e) { LogOnce("voxel queue flush: " + e.Message); }
        }

        private static readonly Dictionary<string, int> _why = new Dictionary<string, int>();
        private static readonly List<NetworkInstanceId> _ghosts = new List<NetworkInstanceId>();

        /// <summary>An entry UNET still lists although the game released the object to its pool (or the pool already reused the GameObject
        /// for another netId). Linear playback keeps such entries forever; they are not world state and must never be spawned or destroyed.</summary>
        internal static bool IsGhost(uint key, NetworkIdentity ni)
        {
            if (ni == null) return true;
            if (ni.netId.Value != key) return true;
            try { return Brix.Game.Components.GameComponentExtender.IsNullOrReleased(ni.gameObject); }
            catch (Exception) { return false; }
        }

        /// <summary>Destroys a live object through UNET; children that the PK keeps are detached first (they stay, their hooks re-parent them).</summary>
        /// <summary>A spawn the game completed (object exists, netId set) but UNET did not list (stale key of the dead copy): list it, or the unit stays unregistered, invisible and is hidden by the orphan sweep.</summary>
        private static void RegisterSpawned(uint id, ref Dictionary<uint, NetworkIdentity> sceneNis, ref int count)
        {
            if (sceneNis == null)
            {
                sceneNis = new Dictionary<uint, NetworkIdentity>();
                foreach (NetworkIdentity x in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
                    if (!x.netId.IsEmpty() && x.gameObject.activeInHierarchy) sceneNis[x.netId.Value] = x;
            }
            NetworkIdentity ni;
            if (!sceneNis.TryGetValue(id, out ni) || ni == null) return;
            ClientScene.objects[ni.netId] = ni;
            count++;
        }

        private static bool Kill(uint id, Dictionary<uint, Obj> want)
        {
            GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
            if (go == null) return false;
            NetworkIdentity[] kids = go.GetComponentsInChildren<NetworkIdentity>(true);
            for (int k = 0; k < kids.Length; k++)
            {
                NetworkIdentity c = kids[k];
                if (c.gameObject != go && want.ContainsKey(c.netId.Value)) c.transform.SetParent(null, true);
            }
            bool ok = BtcReplay.FeedRaw(DestroyMsg(id));
            // an object UNET forgot but did not hide stays forever (user: frozen clone workers/wards): hide it
            return ok;
        }

        /// <summary>netId of the nearest NetworkIdentity above the object (0 = none).</summary>
        private static readonly List<GameObject> _orphans = new List<GameObject>();
        private static int _orphanFrame;
        private static readonly HashSet<uint> _lastPk = new HashSet<uint>();
        private static bool _pkSweep;
        private static int _pkJumpFrame = -1;

        /// <summary>Any seek path (forward seek, legacy rewind) ends here too: sweep leftovers next frame.</summary>
        internal static void ScheduleSweep() { _orphanFrame = Time.frameCount; _orphanAt = 0f; _orphans.Clear(); _pkSweep = _pkJumpFrame == Time.frameCount; }

        private static float _orphanAt;

        /// <summary>
        /// Leftovers of a seek (user: frozen clone workers/wards that no later seek touches) = active networked objects UNET does not list at
        /// all. Two passes so a live object is never hidden (user: invisible bricktrons after the first version): the frame after the seek
        /// (Object.Destroy is deferred) candidates are taken - not registered under ANY key, not just their own netId - and only those
        /// still unregistered and active 0.5 s later go back to their pool or are hidden. A new seek restarts both passes.
        /// </summary>
        private static void SweepOrphans()
        {
            HashSet<GameObject> reg = new HashSet<GameObject>();
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects)
                if (kv.Value != null) reg.Add(kv.Value.gameObject);
            if (_orphanAt == 0f)
            {
                _orphans.Clear();
                foreach (NetworkIdentity x in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
                    if (!x.netId.IsEmpty() && !reg.Contains(x.gameObject)) _orphans.Add(x.gameObject);
                if (_orphans.Count > 0) _orphanAt = Time.unscaledTime + 0.5f;
                return;
            }
            if (Time.unscaledTime < _orphanAt) return;
            _orphanAt = 0f;
            System.Text.StringBuilder sb = null;
            string skipped = null;
            for (int i = 0; i < _orphans.Count; i++)
            {
                GameObject go = _orphans[i];
                if (go == null || !go.activeInHierarchy || reg.Contains(go)) continue;
                NetworkIdentity ni = go.GetComponent<NetworkIdentity>();
                if (ni == null || ni.netId.IsEmpty()) continue;
                // a unit is hidden only when its live copy is confirmed (its netId is registered to another live object; names are not unique): a lone unit UNET
                // lost track of may still be the one the player watches (user: invisible bricktrons that later die)
                GameObject twin = ClientScene.FindLocalObject(ni.netId);
                bool dup = twin != null && twin != go && twin.activeInHierarchy;
                bool unit = go.GetComponent<Brix.Game.AI.Labor>() != null;
                if (unit && !dup && _pkSweep && !_lastPk.Contains(ni.netId.Value)) dup = true; // not in the keyframe: must not be there
                if (unit && !dup) { if (skipped == null) skipped = ""; skipped += Kind(go.name) + "#" + ni.netId.Value + " "; continue; }
                if (sb == null) sb = new System.Text.StringBuilder();
                if (sb.Length < 400) sb.Append(Kind(go.name)).Append('#').Append(ni.netId.Value).Append(", ");
                // hide only: handing it back to the pool could give the same GameObject to a live unit later (invisible bricktrons)
                go.SetActive(false);
            }
            _orphans.Clear();
            if (sb != null) BtcLog.Info("ReplaySeek", "after seek: hid objects UNET no longer lists: " + sb);
            if (skipped != null) BtcLog.Info("ReplaySeek", "after seek: kept unlisted units without a live copy: " + skipped);
        }

        private static uint ParentOf(GameObject go)
        {
            Transform p = go.transform.parent;
            if (p == null) return 0u;
            NetworkIdentity pi = p.GetComponentInParent<NetworkIdentity>();
            return pi != null ? pi.netId.Value : 0u;
        }

        private static List<Obj> ParentsFirst(Obj[] objs, Dictionary<uint, Obj> want)
        {
            List<Obj> order = new List<Obj>(objs.Length);
            HashSet<uint> done = new HashSet<uint>();
            for (int i = 0; i < objs.Length; i++) Visit(objs[i], want, done, order, 0);
            return order;
        }

        private static void Visit(Obj o, Dictionary<uint, Obj> want, HashSet<uint> done, List<Obj> order, int depth)
        {
            if (done.Contains(o.Id)) return;
            done.Add(o.Id);
            Obj p;
            if (o.ParentId != 0u && depth < 32 && want.TryGetValue(o.ParentId, out p)) Visit(p, want, done, order, depth + 1);
            order.Add(o);
        }

        // ---- SyncVar hooks (initial OnDeserialize only assigns fields: a jump replays the hooks like a normal update would) ----

        private sealed class HookVar
        {
            public FieldInfo F;
            public MethodInfo M;
        }

        private static readonly Dictionary<Type, HookVar[]> HookCache = new Dictionary<Type, HookVar[]>();

        private static HookVar[] HooksOf(Type t)
        {
            HookVar[] r;
            if (HookCache.TryGetValue(t, out r)) return r;
            List<HookVar> l = new List<HookVar>();
            for (Type c = t; c != null && c != typeof(NetworkBehaviour); c = c.BaseType)
            {
                FieldInfo[] fs = c.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fs.Length; i++)
                {
                    object[] at = fs[i].GetCustomAttributes(typeof(SyncVarAttribute), false);
                    if (at.Length == 0) continue;
                    string hook = ((SyncVarAttribute)at[0]).hook;
                    if (string.IsNullOrEmpty(hook)) continue;
                    MethodInfo m = null;
                    for (Type mc = c; mc != null && m == null; mc = mc.BaseType)
                    {
                        MethodInfo[] ms = mc.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        for (int k = 0; k < ms.Length; k++)
                            if (ms[k].Name == hook && ms[k].GetParameters().Length == 1) { m = ms[k]; break; }
                    }
                    if (m != null) l.Add(new HookVar { F = fs[i], M = m });
                }
            }
            r = l.ToArray();
            HookCache[t] = r;
            return r;
        }

        private static int DeserializeWithHooks(NetworkIdentity ni, byte[] payload)
        {
            NetworkBehaviour[] nbs = ni.GetComponents<NetworkBehaviour>();
            object[][] olds = new object[nbs.Length][];
            for (int i = 0; i < nbs.Length; i++)
            {
                HookVar[] hv = HooksOf(nbs[i].GetType());
                if (hv.Length == 0) continue;
                olds[i] = new object[hv.Length];
                for (int k = 0; k < hv.Length; k++) olds[i][k] = hv[k].F.GetValue(nbs[i]);
            }
            NetworkReader r = new NetworkReader(payload);
            for (int i = 0; i < nbs.Length; i++) nbs[i].OnDeserialize(r, true);
            // GameObject SyncVars (owner faction, capturers) arrive as netIds and are only resolved here, like on a spawn
            for (int i = 0; i < nbs.Length; i++) nbs[i].PreStartClient();
            int n = 0;
            for (int i = 0; i < nbs.Length; i++)
            {
                if (olds[i] == null) continue;
                HookVar[] hv = HooksOf(nbs[i].GetType());
                for (int k = 0; k < hv.Length; k++)
                {
                    object nv = hv[k].F.GetValue(nbs[i]);
                    if (Equals(nv, olds[i][k])) continue;
                    if (RunHook(nbs[i], hv[k], olds[i][k], nv)) n++;
                }
            }
            return n;
        }

        /// <summary>Re-fire every hook of an object with its current value (used when the parent is still wrong after the restore).</summary>
        private static void ForceHooks(NetworkIdentity ni)
        {
            NetworkBehaviour[] nbs = ni.GetComponents<NetworkBehaviour>();
            for (int i = 0; i < nbs.Length; i++)
            {
                HookVar[] hv = HooksOf(nbs[i].GetType());
                for (int k = 0; k < hv.Length; k++)
                {
                    object v = hv[k].F.GetValue(nbs[i]);
                    Type ft = hv[k].F.FieldType;
                    object blank = ft.IsValueType ? Activator.CreateInstance(ft) : null;
                    RunHook(nbs[i], hv[k], blank, v);
                }
            }
        }

        /// <summary>Field = old, hook(new) like UNET's non-initial deserialize; if the hook does not store the value, store it.</summary>
        private static bool RunHook(NetworkBehaviour nb, HookVar hv, object oldV, object newV)
        {
            try
            {
                hv.F.SetValue(nb, oldV);
                hv.M.Invoke(nb, new object[] { newV });
                if (!Equals(hv.F.GetValue(nb), newV)) hv.F.SetValue(nb, newV);
                return true;
            }
            catch (Exception e)
            {
                hv.F.SetValue(nb, newV);
                LogOnce("hook " + nb.GetType().Name + "." + hv.M.Name + ": " + (e.InnerException != null ? e.InnerException.Message : e.Message));
                return false;
            }
        }

        internal static float[] ReadParams(Animator a)
        {
            AnimatorControllerParameter[] ps = a.parameters;
            float[] v = new float[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                switch (ps[i].type)
                {
                    case AnimatorControllerParameterType.Float: v[i] = a.GetFloat(ps[i].nameHash); break;
                    case AnimatorControllerParameterType.Int: v[i] = a.GetInteger(ps[i].nameHash); break;
                    case AnimatorControllerParameterType.Bool: v[i] = a.GetBool(ps[i].nameHash) ? 1f : 0f; break;
                }
            }
            return v;
        }

        internal static void WriteParams(Animator a, float[] v)
        {
            if (v == null) return;
            AnimatorControllerParameter[] ps = a.parameters;
            for (int i = 0; i < ps.Length && i < v.Length; i++)
            {
                switch (ps[i].type)
                {
                    case AnimatorControllerParameterType.Float: a.SetFloat(ps[i].nameHash, v[i]); break;
                    case AnimatorControllerParameterType.Int: a.SetInteger(ps[i].nameHash, (int)v[i]); break;
                    case AnimatorControllerParameterType.Bool: a.SetBool(ps[i].nameHash, v[i] > 0.5f); break;
                }
            }
        }

        private static string Kind(string n)
        {
            int i = n.IndexOf(" (");
            return (i > 0 ? n.Substring(0, i) : n).Replace("(Clone)", "");
        }

        private static void CountWhy(string k)
        {
            int n; _why.TryGetValue(k, out n); _why[k] = n + 1;
        }

        private static string Why()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, int> kv in _why) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", ");
            return sb.ToString();
        }

        private static void RestoreAnim(NetworkIdentity ni, Obj o)
        {
            try
            {
                UNetAnimator an = ni.GetComponent<UNetAnimator>();
                if (an == null || an.animator == null) return;
                WriteParams(an.animator, o.AnimParams);
                an.animator.SetFloat("CarryMode", o.AnimCarry);
                an.animator.Play(o.AnimState);
                an.animator.Update(0f); // resolve condition transitions of the restored params now, not after the first frame
                if (StateChanged != null) StateChanged.Invoke(an, new object[] { an.state });
            }
            catch (Exception e) { LogOnce("anim restore: " + e.Message); }
        }

        private static byte[] DestroyMsg(uint id)
        {
            NetworkWriter w = new NetworkWriter();
            w.StartMessage(MsgType.ObjectDestroy);
            w.Write(new NetworkInstanceId(id));
            w.FinishMessage();
            return w.ToArray();
        }

        /// <summary>Recorded spawn message (3 ObjectSpawn / 10 ObjectSpawnScene) with the PK pose and payload.</summary>
        private static byte[] RewriteSpawn(byte[] src, Obj o)
        {
            try
            {
                NetworkReader r = new NetworkReader(src);
                r.ReadUInt16();
                short type = (short)r.ReadUInt16();
                NetworkInstanceId nid = r.ReadNetworkId();
                NetworkWriter w = new NetworkWriter();
                w.StartMessage(type);
                w.Write(nid);
                if (type == MsgType.ObjectSpawn)
                {
                    NetworkHash128 asset = r.ReadNetworkHash128();
                    r.ReadVector3();
                    r.ReadBytesAndSize();
                    w.Write(asset);
                    w.Write(o.Pos);
                    w.WriteBytesFull(o.Payload);
                    if (r.Position + 16 <= src.Length) w.Write(o.Rot);
                }
                else
                {
                    NetworkSceneId sid = r.ReadSceneId();
                    w.Write(sid);
                    w.Write(o.Pos);
                    w.WriteBytesFull(o.Payload);
                }
                w.FinishMessage();
                return w.ToArray();
            }
            catch (Exception e) { LogOnce("rewrite spawn " + o.Id + ": " + e.Message); return null; }
        }

        // ---------------- first-open preprocess ----------------

        // ---- quiet preprocess: cameras draw nothing, game audio muted, only a plate with the progress ----

        private sealed class QuietCam
        {
            public Camera Cam;
            public int Mask;
            public CameraClearFlags Flags;
            public Color Color;
            public bool Kept;
        }

        private static readonly List<QuietCam> _quiet = new List<QuietCam>();
        private static bool _quietOn, _muteWas;
        private static float _volWas;
        private static string _plate;
        private static GameObject _plateGo;

        private static void QuietBegin()
        {
            if (_quietOn) return;
            _quietOn = true;
            try
            {
                // cameras keep rendering: with them off the terrain changes were not applied until the end, so every keyframe
                // captured the baseline terrain (user: start of the replay showed the whole game's terraforming). The plate covers the screen.
                _quiet.Clear();
                _volWas = AudioListener.volume;
                AudioListener.volume = 0f;
                _muteWas = Brix.Audio.SoundEngine.mute;
                Brix.Audio.SoundEngine.mute = true; // Wwise ignores AudioListener
                if (_plateGo == null)
                {
                    _plateGo = new GameObject("BtcReplaySeekPlate");
                    UnityEngine.Object.DontDestroyOnLoad(_plateGo);
                    _plateGo.AddComponent<BtcReplaySeekPlate>();
                }
                _plateGo.SetActive(true);
            }
            catch (Exception e) { BtcLog.Error("ReplaySeek", "quiet begin: " + e.Message); }
        }

        private static void QuietEnd()
        {
            if (!_quietOn) return;
            _quietOn = false;
            _plate = null;
            try
            {
                for (int i = 0; i < _quiet.Count; i++)
                {
                    QuietCam q = _quiet[i];
                    if (q.Cam == null) continue;
                    if (q.Kept)
                    {
                        q.Cam.cullingMask = q.Mask;
                        q.Cam.clearFlags = q.Flags;
                        q.Cam.backgroundColor = q.Color;
                    }
                    else q.Cam.enabled = true;
                }
                _quiet.Clear();
                AudioListener.volume = _volWas;
                Brix.Audio.SoundEngine.mute = _muteWas;
                if (_plateGo != null) _plateGo.SetActive(false);
            }
            catch (Exception e) { BtcLog.Error("ReplaySeek", "quiet end: " + e.Message); }
        }

        internal static string Plate { get { return _quietOn ? _plate : null; } }

        /// <summary>Progress 0..1 of the keyframe preparation pass (BtcReplayLoadUI).</summary>
        internal static float PrepFraction;

        /// <summary>The first keyframe pass has not started yet but will (long replay, no cache): the loading screen stays up so the world is not shown in between.</summary>
        internal static bool Settling { get { EnsureCfg(); return _state == 0 && _enabled && !_failed && _preprocess && !DevNoPreprocess && BtcReplay.Mode == BtcReplay.State.Playing && BtcReplay.PlayTotal >= 20f && !BtcReplay.FeedEnded; } }

        // objects the keyframe had hidden (stored/carried items, wards): spawned ones are re-shown by the game a frame or two later;
        // keep them hidden for a short window right after the jump (20 frames and at most 0.3 s of replay time)
        private static readonly List<GameObject> _keepOff = new List<GameObject>(512);
        private static readonly Dictionary<string, int> _keepOffWhy = new Dictionary<string, int>();
        private static int _keepOffFrames;
        private static long _keepOffUntilMs;

        private static void KeepOffTick()
        {
            if (_orphanFrame > 0 && Time.frameCount > _orphanFrame) { _orphanFrame = 0; _orphanAt = 0f; SweepOrphans(); }
            else if (_orphanAt > 0f && Time.unscaledTime >= _orphanAt) SweepOrphans();
            if (_keepOffFrames <= 0) return;
            _keepOffFrames--;
            bool end = _keepOffFrames == 0 || BtcReplay.FeedMs > _keepOffUntilMs;
            if (!end)
            {
                for (int i = 0; i < _keepOff.Count; i++)
                {
                    GameObject go = _keepOff[i];
                    if (go == null || !go.activeSelf) continue;
                    go.SetActive(false);
                    string k = Kind(go.name);
                    int n; _keepOffWhy.TryGetValue(k, out n); _keepOffWhy[k] = n + 1;
                }
                return;
            }
            _keepOffFrames = 0;
            _keepOff.Clear();
            if (_keepOffWhy.Count > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, int> kv in _keepOffWhy) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", ");
                BtcLog.Info("ReplaySeek", "after jump: kept hidden " + sb);
            }
        }

        private static volatile string _cacheMsg;

        internal static void Tick(float now, bool seeking, float clock, float total)
        {
            string cm = _cacheMsg;
            if (cm != null) { _cacheMsg = null; BtcLog.Info("ReplaySeek", cm); }
            KeepOffTick();
            if (_failed) return;
            EnsureCfg();
            if (!_enabled) return;
            if (_state == 0)
            {
                if (!_preprocess || DevNoPreprocess || now < _nextCheck) return;
                _nextCheck = now + 0.5f;
                if (!BtcReplay.FeedGo || seeking || BtcReplay.FeedRewinding || BtcReplayAnchor.Busy || BtcReplay.FeedEnded) return;
                if (Pks.Count == 0 || total < 20f) return; // need the first PK (world ready) before running ahead
                if (LoadCache())
                {
                    _state = 2;
                    return;
                }
                _state = 1;
                _origin = clock;
                _ppT0 = Time.realtimeSinceStartup;
                BtcLog.Info("ReplaySeek", "preprocess: " + clock.ToString("0.0") + " -> " + total.ToString("0.0") + " s");
                QuietBegin();
                _plate = BtcLang.Russian ? "Подготовка реплея для быстрой перемотки... 0%" : "Preparing the replay for instant seek... 0%";
                BtcReplay.SeekForwardRaw(total);
            }
            else if (_state == 1)
            {
                if (seeking)
                {
                    int pct = total > 0f ? Mathf.Clamp((int)(100f * clock / total), 0, 100) : 0;
                    _plate = (BtcLang.Russian ? "Подготовка реплея для быстрой перемотки... " : "Preparing the replay for instant seek... ") + pct + "%";
                    PrepFraction = pct / 100f;
                    return;
                }
                _state = 2;
                QuietEnd();
                BtcLog.Info("ReplaySeek", "preprocess done in " + (Time.realtimeSinceStartup - _ppT0).ToString("0.0") + " s: " + Pks.Count + " pks, "
                    + (_bytes / 1024) + " KB, " + SpawnMsgs.Count + " spawn msgs, avg capture " + (_capN > 0 ? _capMs / _capN : 0).ToString("0.0") + " ms");
                SaveCacheAsync();
                Pk back = Best((long)(_origin * 1000f));
                if (back == null || !Jump(back, (long)(_origin * 1000f), true)) BtcLog.Warn("ReplaySeek", "preprocess: jump back to " + _origin.ToString("0.0") + " s not possible");
            }
        }

        // ---------------- disk cache (<BetterThanCastle>/cash/*.btcs) ----------------

        private const int CacheMagic = 0x53435442; // "BTCS"
        private const int CacheVersion = 22;

        /// <summary>True when the keyframe cache of a replay file exists (replay list: "keyframes ready").</summary>
        internal static bool HasCacheFor(string f)
        {
            try
            {
                if (string.IsNullOrEmpty(f) || !System.IO.File.Exists(f)) return false;
                System.IO.FileInfo fi = new System.IO.FileInfo(f);
                string name = System.IO.Path.GetFileNameWithoutExtension(f);
                char[] bad = System.IO.Path.GetInvalidFileNameChars();
                for (int i = 0; i < bad.Length; i++) name = name.Replace(bad[i], '_');
                if (name.Length > 60) name = name.Substring(0, 60);
                string dir = System.IO.Path.Combine(BtcPaths.Root, "cash");
                return System.IO.File.Exists(System.IO.Path.Combine(dir, name + "_" + fi.Length.ToString("x") + "_" + fi.LastWriteTimeUtc.Ticks.ToString("x") + ".btcs"));
            }
            catch (Exception) { return false; }
        }

        private static string CachePath()
        {
            string f = BtcReplay.FeedFile;
            if (string.IsNullOrEmpty(f) || !System.IO.File.Exists(f)) return null;
            System.IO.FileInfo fi = new System.IO.FileInfo(f);
            string name = System.IO.Path.GetFileNameWithoutExtension(f);
            char[] bad = System.IO.Path.GetInvalidFileNameChars();
            for (int i = 0; i < bad.Length; i++) name = name.Replace(bad[i], '_');
            if (name.Length > 60) name = name.Substring(0, 60);
            string dir = System.IO.Path.Combine(BtcPaths.Root, "cash");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, name + "_" + fi.Length.ToString("x") + "_" + fi.LastWriteTimeUtc.Ticks.ToString("x") + ".btcs");
        }

        private static void SaveCacheAsync()
        {
            string path;
            try { path = CachePath(); }
            catch (Exception e) { LogOnce("cache path: " + e.Message); return; }
            if (path == null || _failed) return;
            List<Pk> pks = new List<Pk>(Pks.Values);
            Dictionary<uint, byte[]> spawns = new Dictionary<uint, byte[]>(SpawnMsgs);
            List<StatsRec> stats = new List<StatsRec>(StatsMsgs);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    string tmp = path + ".tmp";
                    using (System.IO.FileStream fs = new System.IO.FileStream(tmp, System.IO.FileMode.Create))
                    using (System.IO.Compression.DeflateStream ds = new System.IO.Compression.DeflateStream(fs, System.IO.Compression.CompressionMode.Compress))
                    using (System.IO.BinaryWriter bw = new System.IO.BinaryWriter(ds))
                    {
                        bw.Write(CacheMagic);
                        bw.Write(CacheVersion);
                        // shared terrain/nature blobs are written once
                        List<byte[]> blobs = new List<byte[]>();
                        Dictionary<byte[], int> blobIdx = new Dictionary<byte[], int>();
                        for (int i = 0; i < pks.Count; i++)
                        {
                            AddBlob(blobs, blobIdx, pks[i].Terrain);
                            AddBlob(blobs, blobIdx, pks[i].Nature);
                        }
                        bw.Write(blobs.Count);
                        for (int i = 0; i < blobs.Count; i++) { bw.Write(blobs[i].Length); bw.Write(blobs[i]); }
                        bw.Write(spawns.Count);
                        foreach (KeyValuePair<uint, byte[]> kv in spawns) { bw.Write(kv.Key); bw.Write(kv.Value.Length); bw.Write(kv.Value); }
                        bw.Write(stats.Count);
                        for (int i = 0; i < stats.Count; i++) { bw.Write(stats[i].Ms); bw.Write(stats[i].Raw.Length); bw.Write(stats[i].Raw); }
                        bw.Write(pks.Count);
                        for (int i = 0; i < pks.Count; i++)
                        {
                            Pk p = pks[i];
                            bw.Write(p.Slot); bw.Write(p.Ms); bw.Write(p.Pos); bw.Write(p.Hash);
                            bw.Write(p.Terrain != null ? blobIdx[p.Terrain] : -1);
                            bw.Write(p.Nature != null ? blobIdx[p.Nature] : -1);
                            bw.Write(p.Debloc != null ? p.Debloc.Length : -1);
                            if (p.Debloc != null) bw.Write(p.Debloc);
                            bw.Write(p.Visuals != null ? p.Visuals.Length : -1);
                            if (p.Visuals != null) bw.Write(p.Visuals);
                            bw.Write(p.Objs.Length);
                            for (int k = 0; k < p.Objs.Length; k++)
                            {
                                Obj o = p.Objs[k];
                                bw.Write(o.Id);
                                bw.Write(o.Pos.x); bw.Write(o.Pos.y); bw.Write(o.Pos.z);
                                bw.Write(o.Rot.x); bw.Write(o.Rot.y); bw.Write(o.Rot.z); bw.Write(o.Rot.w);
                                bw.Write((byte)((o.Root ? 1 : 0) | (o.Scene ? 2 : 0) | (o.HasAnim ? 4 : 0) | (o.Active ? 8 : 0)));
                                if (o.HasAnim) { bw.Write(o.AnimState); bw.Write(o.AnimCarry); }
                                bw.Write(o.ParentId);
                                int npar = o.AnimParams != null ? o.AnimParams.Length : 0;
                                bw.Write(npar);
                                for (int q = 0; q < npar; q++) bw.Write(o.AnimParams[q]);
                                bw.Write(o.Payload.Length); bw.Write(o.Payload);
                            }
                        }
                    }
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    System.IO.File.Move(tmp, path);
                    _cacheMsg = "cache saved (" + (new System.IO.FileInfo(path).Length / 1024) + " KB, " + sw.ElapsedMilliseconds + " ms): " + System.IO.Path.GetFileName(path);
                }
                catch (Exception e) { _cacheMsg = "cache save failed: " + e.Message; }
            });
        }

        private static void AddBlob(List<byte[]> blobs, Dictionary<byte[], int> idx, byte[] b)
        {
            if (b == null || idx.ContainsKey(b)) return;
            idx[b] = blobs.Count;
            blobs.Add(b);
        }

        private static bool LoadCache()
        {
            string path;
            try { path = CachePath(); }
            catch (Exception) { return false; }
            if (path == null || !System.IO.File.Exists(path)) return false;
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                Dictionary<int, Pk> pks = new Dictionary<int, Pk>();
                Dictionary<uint, byte[]> spawns = new Dictionary<uint, byte[]>();
                List<StatsRec> stats = new List<StatsRec>();
                long bytes = 0;
                using (System.IO.FileStream fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                using (System.IO.Compression.DeflateStream ds = new System.IO.Compression.DeflateStream(fs, System.IO.Compression.CompressionMode.Decompress))
                using (System.IO.BinaryReader br = new System.IO.BinaryReader(ds))
                {
                    if (br.ReadInt32() != CacheMagic || br.ReadInt32() != CacheVersion) return false;
                    int nb = br.ReadInt32();
                    byte[][] blobs = new byte[nb][];
                    for (int i = 0; i < nb; i++) { blobs[i] = br.ReadBytes(br.ReadInt32()); bytes += blobs[i].Length; }
                    int ns = br.ReadInt32();
                    for (int i = 0; i < ns; i++) { uint id = br.ReadUInt32(); spawns[id] = br.ReadBytes(br.ReadInt32()); }
                    int nst = br.ReadInt32();
                    for (int i = 0; i < nst; i++) { StatsRec sr = new StatsRec(); sr.Ms = br.ReadInt64(); sr.Raw = br.ReadBytes(br.ReadInt32()); stats.Add(sr); }
                    int np = br.ReadInt32();
                    for (int i = 0; i < np; i++)
                    {
                        Pk p = new Pk();
                        p.Slot = br.ReadInt32(); p.Ms = br.ReadInt64(); p.Pos = br.ReadInt32(); p.Hash = br.ReadUInt32();
                        int ti = br.ReadInt32(), ni = br.ReadInt32();
                        p.Terrain = ti >= 0 ? blobs[ti] : null;
                        p.Nature = ni >= 0 ? blobs[ni] : null;
                        int dl = br.ReadInt32();
                        p.Debloc = dl >= 0 ? br.ReadBytes(dl) : null;
                        int vl = br.ReadInt32();
                        p.Visuals = vl >= 0 ? br.ReadBytes(vl) : null;
                        p.Objs = new Obj[br.ReadInt32()];
                        for (int k = 0; k < p.Objs.Length; k++)
                        {
                            Obj o = new Obj();
                            o.Id = br.ReadUInt32();
                            o.Pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            o.Rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            byte fl = br.ReadByte();
                            o.Root = (fl & 1) != 0; o.Scene = (fl & 2) != 0; o.HasAnim = (fl & 4) != 0; o.Active = (fl & 8) != 0;
                            if (o.HasAnim) { o.AnimState = br.ReadInt32(); o.AnimCarry = br.ReadSingle(); }
                            o.ParentId = br.ReadUInt32();
                            int npar = br.ReadInt32();
                            if (npar > 0) { o.AnimParams = new float[npar]; for (int q = 0; q < npar; q++) o.AnimParams[q] = br.ReadSingle(); }
                            o.Payload = br.ReadBytes(br.ReadInt32());
                            bytes += o.Payload.Length + 48;
                            p.Objs[k] = o;
                        }
                        pks[p.Slot] = p;
                    }
                }
                Pks.Clear();
                foreach (KeyValuePair<int, Pk> kv in pks) Pks[kv.Key] = kv.Value;
                foreach (KeyValuePair<uint, byte[]> kv in spawns) SpawnMsgs[kv.Key] = kv.Value;
                StatsMsgs.Clear(); StatsMsgs.AddRange(stats); _statsLastMs = stats.Count > 0 ? stats[stats.Count - 1].Ms : -1;
                _bytes = bytes;
                BtcLog.Info("ReplaySeek", "cache loaded in " + sw.ElapsedMilliseconds + " ms: " + Pks.Count + " pks, " + (bytes / 1024) + " KB, " + spawns.Count + " spawn msgs");
                return true;
            }
            catch (Exception e)
            {
                BtcLog.Warn("ReplaySeek", "cache load failed (rebuilding): " + e.Message);
                return false;
            }
        }

        // ---------------- helpers ----------------

        private static bool Same(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static uint HashObjs(Obj[] objs)
        {
            uint h = 2166136261u;
            for (int i = 0; i < objs.Length; i++)
            {
                h = (h ^ objs[i].Id) * 16777619u;
                byte[] p = objs[i].Payload;
                for (int k = 0; k < p.Length; k++) h = (h ^ p[k]) * 16777619u;
            }
            return h;
        }

        /// <summary>DEV verify: hash of the live world in the PK format (compare with the PK captured at the same stream position).</summary>
        internal static string Verify()
        {
            int slot = (int)(BtcReplay.FeedMs / SlotMs);
            Pk pk;
            if (!Pks.TryGetValue(slot, out pk) || pk.Ms != BtcReplay.FeedMs) return "no pk at the current position (" + BtcReplay.FeedMs + " ms)";
            List<Obj> live = new List<Obj>();
            NetworkWriter w = new NetworkWriter();
            Dictionary<uint, Obj> m = new Dictionary<uint, Obj>();
            for (int i = 0; i < pk.Objs.Length; i++) m[pk.Objs[i].Id] = pk.Objs[i];
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects)
            {
                if (kv.Value == null || IsGhost(kv.Key.Value, kv.Value)) continue;
                Obj o = new Obj();
                o.Id = kv.Key.Value;
                // the live animator pose is only a capture-time hint (Play() applies next frame): compare the synced state alone
                Obj po;
                UNetAnimator an = kv.Value.GetComponent<UNetAnimator>();
                if (an != null && m.TryGetValue(o.Id, out po) && po.HasAnim)
                {
                    an.NetworkserverAnimatorStateOnSpawn = po.AnimState;
                    an.NetworkserverAnimatorCarryModeOnSpawn = po.AnimCarry;
                }
                o.Payload = Serialize(kv.Value, w);
                live.Add(o);
            }
            live.Sort((a, b) => a.Id.CompareTo(b.Id));
            Obj[] arr = live.ToArray();
            uint h = HashObjs(arr);
            int diff = 0;
            string first = "";
            for (int i = 0; i < arr.Length; i++)
            {
                Obj o;
                if (!m.TryGetValue(arr[i].Id, out o) || !Same(o.Payload, arr[i].Payload))
                {
                    diff++;
                    if (first.Length < 300)
                    {
                        GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(arr[i].Id));
                        first += (go != null ? go.name : "?") + (o == null ? "(new)" : "[" + DiffPart(go, o.Payload) + "]") + "; ";
                    }
                }
            }
            byte[] tnow = BtcReplayAnchor.TerrainBlob();
            string terr = Same(tnow, pk.Terrain) ? "terrain MATCH" : ("terrain DIFF " + (tnow != null ? tnow.Length : -1) + "/" + (pk.Terrain != null ? pk.Terrain.Length : -1) + " B");
            return "slot " + slot + " " + terr + ", hash " + (h == pk.Hash ? "MATCH" : "DIFF") + " live " + arr.Length + " pk " + pk.Objs.Length + " diff " + diff + " " + first;
        }

        /// <summary>DEV: which NetworkBehaviour segment of the live object differs from the PK payload first.</summary>
        private static string DiffPart(GameObject go, byte[] pk)
        {
            if (go == null) return "?";
            NetworkBehaviour[] nbs = go.GetComponents<NetworkBehaviour>();
            NetworkWriter w = new NetworkWriter();
            int off = 0;
            for (int i = 0; i < nbs.Length; i++)
            {
                w.SeekZero();
                nbs[i].OnSerialize(w, true);
                byte[] seg = w.ToArray();
                bool ok = off + seg.Length <= pk.Length;
                for (int k = 0; ok && k < seg.Length; k++) if (pk[off + k] != seg[k]) ok = false;
                if (!ok) return nbs[i].GetType().Name + "@" + off + "/" + seg.Length;
                off += seg.Length;
            }
            return off == pk.Length ? "none" : "len " + off + "/" + pk.Length;
        }

        private static int _logOnce;

        private static void LogOnce(string s)
        {
            if (_logOnce++ < 20) BtcLog.Warn("ReplaySeek", s);
        }
    }
}
