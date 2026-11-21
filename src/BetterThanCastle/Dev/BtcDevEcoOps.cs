#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using Brix.Game.Network;
using Brix.Game.Semantique;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV LuminaBuild helpers (docs/eco/DESIGN.md): econoore, ecoauto, ecoplace, ecobpcost, ecoplay.
    /// Busy = a long operation is running (BtcDevCmd "eco" wait).
    /// </summary>
    internal sealed class BtcDevEcoOps : MonoBehaviour
    {
        internal static bool Busy;
        internal static bool NoOre;
        private static BtcDevEcoOps _i;
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        private bool _auto;
        private float _nextAuto;
        private int _restoreFrames;
        private float _savedScale = 1f;
        private bool _placeBusy;

        private sealed class PlayLine { public float T; public string Raw; public string Kind; }
        private readonly List<PlayLine> _play = new List<PlayLine>();
        private int _pi;
        private float _pt0;
        private bool _playing;
        private readonly Dictionary<long, uint> _map = new Dictionary<long, uint>();
        private readonly List<long> _traceBp = new List<long>();
        private int _bpNext;
        private readonly HashSet<uint> _bpSeen = new HashSet<uint>();
        private int _playOk, _playFail;
        private readonly Dictionary<string, int> _unsup = new Dictionary<string, int>();

        private static BtcDevEcoOps Inst()
        {
            if (_i == null)
            {
                GameObject go = new GameObject("BtcDevEcoOps");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _i = go.AddComponent<BtcDevEcoOps>();
            }
            return _i;
        }

        private static float F(string s) { float f; return float.TryParse(s, System.Globalization.NumberStyles.Float, Inv, out f) ? f : 0f; }

        // ------------------------------------------------------------ units
        internal static List<Labor> Units()
        {
            List<Labor> r = new List<Labor>();
            User u = null; try { u = User.LocalUser; } catch { }
            Labor[] all = UnityEngine.Object.FindObjectsOfType<Labor>();
            for (int i = 0; i < all.Length; i++)
            {
                Labor l = all[i];
                if (l == null || l.GetComponent<NetworkIdentity>() == null) continue;
                try { if (u != null && u.faction != null && !u.faction.IsSame(l.gameObject)) continue; } catch { }
                r.Add(l);
            }
            r.Sort(delegate (Labor a, Labor b) { return a.GetComponent<NetworkIdentity>().netId.Value.CompareTo(b.GetComponent<NetworkIdentity>().netId.Value); });
            return r;
        }

        private static uint IdOf(Component c) { return c.GetComponent<NetworkIdentity>().netId.Value; }

        // ------------------------------------------------------------ econoore
        private static bool _patched;
        private static void EnsurePatch()
        {
            if (_patched) return;
            _patched = true;
            try
            {
                HarmonyLib.Harmony h = new HarmonyLib.Harmony("btc.dev.noore");
                MethodInfo orig = typeof(Brix.Engine.FlatWorld.Layers.DeepLayer).GetMethod("GetDeepType");
                HarmonyLib.HarmonyMethod pf = new HarmonyLib.HarmonyMethod(typeof(BtcDevNoOrePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
                pf.priority = HarmonyLib.Priority.Last;
                h.Patch(orig, null, pf, null);
                BtcLog.Info("EcoOre", "DeepLayer.GetDeepType postfix installed");
            }
            catch (Exception e) { BtcLog.Error("EcoOre", "patch failed: " + e); }
        }

        internal static void Ore(string[] p)
        {
            if (p != null && p.Length > 1) { NoOre = F(p[1]) > 0.5f || p[1] == "on"; if (NoOre) EnsurePatch(); BtcLog.Info("EcoOre", "NoOre=" + NoOre + " (affects voxel generation from now on)"); return; }
            NoOre = true; EnsurePatch();
            int replaced = 0;
            try
            {
                if (!VoxelEngine.Ready) { BtcLog.Warn("EcoOre", "engine not ready"); return; }
                if (replaced == 0 || true)
                {
                    StringBuilder ms = new StringBuilder();
                    foreach (MethodInfo mm in Voxel.Engine.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        string n = mm.Name;
                        if (n.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Dirty", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mesh", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Rebuild", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Commit", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Flush", StringComparison.OrdinalIgnoreCase) >= 0)
                            ms.Append(n).Append('(').Append(mm.GetParameters().Length).Append(") ");
                    }
                    BtcLog.Info("EcoOre", "engine " + Voxel.Engine.GetType().FullName + " methods: " + ms);
                }
                Bounds bb = Voxel.Engine.ComputeOccupiedBounds();
                int x0 = Mathf.FloorToInt(bb.min.x), x1 = Mathf.CeilToInt(bb.max.x) - 1;
                int z0 = Mathf.FloorToInt(bb.min.z), z1 = Mathf.CeilToInt(bb.max.z) - 1;
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        int b, t;
                        VoxelEngine.GetColumnData(x, z, out b, out t);
                        if (t < b) continue;
                        for (int y = b; y <= t; y++)
                        {
                            XYZ pos = new XYZ(x, y, z);
                            VoxelHandle h = Voxel.Engine.GetVoxelHandle(pos);
                            int ty = Voxel.Engine.GetVoxelType(h);
                            if (ty != 54 && ty != 44 && ty != 38 && ty != 55 && ty != 57 && ty != 59) continue;
                            if (!Voxel.IsTerrain(pos)) continue;
                            Voxel.Engine.SetVoxelType(h, 8);
                            replaced++;
                            if (replaced <= 5)
                            {
                                int back = Voxel.Engine.GetVoxelType(Voxel.Engine.GetVoxelHandle(pos));
                                int bid = -1; try { using (IVoxel vv = Voxel.GetVoxel(pos)) { bid = vv.BlockInfo != null ? vv.BlockInfo.ID : -2; } } catch { }
                                BtcLog.Info("EcoOre", "sample " + pos.x + "," + pos.y + "," + pos.z + " was " + ty + " now type " + back + " blockinfo " + bid);
                            }
                        }
                    }
            }
            catch (Exception e) { BtcLog.Error("EcoOre", "failed: " + e); }
            BtcLog.Info("EcoOre", "replaced " + replaced);
            Inst().StartCoroutine(Inst().OreVerify());
        }

        private static int CountOre()
        {
            int n = 0;
            Bounds bb = Voxel.Engine.ComputeOccupiedBounds();
            int x0 = Mathf.FloorToInt(bb.min.x), x1 = Mathf.CeilToInt(bb.max.x) - 1;
            int z0 = Mathf.FloorToInt(bb.min.z), z1 = Mathf.CeilToInt(bb.max.z) - 1;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    int b, t;
                    VoxelEngine.GetColumnData(x, z, out b, out t);
                    for (int y = b; y <= t; y++)
                    {
                        int ty = Voxel.Engine.GetVoxelType(Voxel.Engine.GetVoxelHandle(new XYZ(x, y, z)));
                        if (ty == 54 || ty == 44 || ty == 38 || ty == 55 || ty == 57 || ty == 59) n++;
                    }
                }
            return n;
        }

        private IEnumerator OreVerify()
        {
            float[] w = { 0.5f, 2f, 6f };
            for (int i = 0; i < w.Length; i++)
            {
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < w[i]) yield return null;
                BtcLog.Info("EcoOre", "verify after " + w[i] + " s: remaining ore voxels " + CountOre());
            }
        }

        // ------------------------------------------------------------ ecoauto
        internal static void Auto(bool on)
        {
            Inst()._auto = on;
            BtcLog.Info("EcoAuto", on ? "on (waitForOrder at 1 Hz)" : "off");
            if (on) Inst().Assert();
        }

        private void Assert()
        {
            int n = 0;
            List<Labor> us = Units();
            for (int i = 0; i < us.Count; i++)
            {
                try
                {
                    if (us[i].Autonomy.Id != AutonomyStatus.Ids.waitForOrder) { us[i].Autonomy = AutonomyStatus.FromId(AutonomyStatus.Ids.waitForOrder); n++; }
                }
                catch (Exception e) { BtcLog.Warn("EcoAuto", e.Message); }
            }
            if (n > 0) BtcLog.Info("EcoAuto", "reset " + n + " of " + us.Count + " units");
        }

        private void Update()
        {
            if (_auto && Time.unscaledTime >= _nextAuto) { _nextAuto = Time.unscaledTime + 1f; Assert(); }
            if (_placeBusy)
            {
                if (--_restoreFrames <= 0)
                {
                    Time.timeScale = _savedScale;
                    _placeBusy = false; Busy = false;
                    BtcLog.Info("EcoPlace", "resumed timescale " + _savedScale);
                }
            }
            if (_playing) PlayTick();
        }

        // ------------------------------------------------------------ ecoplace
        /// <summary>"near [r]" = ring of radius r around the unit centroid; "x,y,z;x,y,z" with y=-1 -> surface. Units in netId order.</summary>
        internal static void Place(string[] p)
        {
            try
            {
                List<Labor> us = Units();
                List<Vector3> target = new List<Vector3>();
                if (p.Length > 1 && p[1] == "near")
                {
                    float r = p.Length > 2 ? F(p[2]) : 4f;
                    Vector3 c = Vector3.zero;
                    for (int i = 0; i < us.Count; i++) c += us[i].transform.position;
                    if (us.Count > 0) c /= us.Count;
                    for (int i = 0; i < us.Count; i++)
                    {
                        float a = i * Mathf.PI * 2f / Mathf.Max(1, us.Count);
                        int x = Mathf.FloorToInt(c.x + Mathf.Cos(a) * r), z = Mathf.FloorToInt(c.z + Mathf.Sin(a) * r);
                        target.Add(new Vector3(x, -1f, z));
                    }
                }
                else if (p.Length > 1)
                {
                    string[] cells = p[1].Split(';');
                    for (int i = 0; i < cells.Length; i++)
                    {
                        string[] v = cells[i].Split(',');
                        if (v.Length < 3) continue;
                        target.Add(new Vector3(F(v[0]), F(v[1]), F(v[2])));
                    }
                }
                List<uint> ids = new List<uint>();
                for (int i = 0; i < us.Count && i < target.Count; i++) ids.Add(IdOf(us[i]));
                ApplyPlace(ids, target);
            }
            catch (Exception e) { BtcLog.Error("EcoPlace", "failed: " + e); }
        }

        private static void ApplyPlace(List<uint> ids, List<Vector3> cells)
        {
            BtcDevEcoOps o = Inst();
            if (!o._placeBusy) { o._savedScale = Time.timeScale > 0f ? Time.timeScale : 1f; }
            Time.timeScale = 0f;
            for (int i = 0; i < ids.Count && i < cells.Count; i++)
            {
                NetworkIdentity ni;
                if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(ids[i]), out ni) || ni == null) { BtcLog.Warn("EcoPlace", "no unit " + ids[i]); continue; }
                Vector3 c = cells[i];
                int x = Mathf.FloorToInt(c.x), z = Mathf.FloorToInt(c.z), y = Mathf.RoundToInt(c.y);
                if (y < 0) y = VoxelEngine.GetColumnTop(x, z) - 1;
                Vector3 w = new Vector3(x + 0.5f, y + 1.02f, z + 0.5f);
                Labor l = ni.GetComponent<Labor>();
                try { Order.Move.HaltWhatever.Send(l, Labor.DecisionSource.User); } catch { }
                Locomotion4 loc = ni.GetComponent<Locomotion4>();
                if (loc != null) loc.SetPosition(w, true); else ni.transform.position = w;
                BtcDevEcoTrace.Line("PLACE", ids[i] + "|" + x + "," + y + "," + z);
                BtcLog.Info("EcoPlace", "unit " + ids[i] + " -> " + x + "," + y + "," + z);
            }
            o._placeBusy = true; o._restoreFrames = 3; Busy = true;
        }

        // ------------------------------------------------------------ ecobpcost
        internal static void BpCost(string[] p)
        {
            if (p.Length < 2) { BtcLog.Warn("EcoBp", "usage: ecobpcost Factory.Name,Factory.Name"); return; }
            Busy = true;
            Inst().StartCoroutine(Inst().BpRun(p[1].Split(',')));
        }

        private IEnumerator BpRun(string[] keys)
        {
            BtcDevEcoTrace.Mute++;
            for (int k = 0; k < keys.Length; k++)
            {
                string key = keys[k].Trim();
                if (key.Length == 0) continue;
                string fac = "Blueprints", nm = key;
                int dot = key.IndexOf('.');
                if (dot > 0) { fac = key.Substring(0, dot); nm = key.Substring(dot + 1); }
                else key = fac + "." + nm;
                string result = null;
                IEnumerator it = BpOne(fac, nm, key);
                while (true)
                {
                    bool more;
                    try { more = it.MoveNext(); }
                    catch (Exception e) { result = "bpcost|" + key + "|ERR " + e.Message + "|"; break; }
                    if (!more) break;
                    object cur = it.Current;
                    if (cur is string) { result = (string)cur; break; }
                    yield return cur;
                }
                if (result == null) result = "bpcost|" + key + "|?|";
                BtcLog.Info("EcoBp", result);
            }
            BtcDevEcoTrace.Mute--;
            Busy = false;
        }

        private IEnumerator BpOne(string fac, string nm, string key)
        {
            User u = User.LocalUser;
            UNetBlueprint ub = u.GetComponent<UNetBlueprint>();
            Vector3 anchor = Vector3.zero;
            List<Labor> us = Units();
            for (int i = 0; i < us.Count; i++) anchor += us[i].transform.position;
            if (us.Count > 0) anchor /= us.Count;
            // far from units: ring of candidates 25..45 voxels away
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float ang = (attempt * 137.5f) * Mathf.Deg2Rad;
                float rad = 22f + (attempt % 8) * 3f;
                int x = Mathf.FloorToInt(anchor.x + Mathf.Cos(ang) * rad), z = Mathf.FloorToInt(anchor.z + Mathf.Sin(ang) * rad);
                int top;
                try { top = VoxelEngine.GetColumnTop(x, z); } catch { continue; }
                if (top <= 0) continue;
                Vector3 pos = new Vector3(x, top, z);
                HashSet<uint> before = new HashSet<uint>();
                Blueprint[] pre = UnityEngine.Object.FindObjectsOfType<Blueprint>();
                for (int i = 0; i < pre.Length; i++) before.Add(IdOf(pre[i]));
                ub.CmdSpawnPlacedBlueprint(fac, nm, pos, Quaternion.identity, false, null, false);
                for (int f = 0; f < 15; f++) yield return null;
                List<Blueprint> fresh = new List<Blueprint>();
                Blueprint[] post = UnityEngine.Object.FindObjectsOfType<Blueprint>();
                for (int i = 0; i < post.Length; i++) { NetworkIdentity ni = post[i].GetComponent<NetworkIdentity>(); if (ni != null && !before.Contains(ni.netId.Value)) fresh.Add(post[i]); }
                if (fresh.Count == 0) continue;
                // compounds place children over a few frames
                for (int f = 0; f < 45; f++) yield return null;
                post = UnityEngine.Object.FindObjectsOfType<Blueprint>();
                fresh.Clear();
                for (int i = 0; i < post.Length; i++) { NetworkIdentity ni = post[i].GetComponent<NetworkIdentity>(); if (ni != null && !before.Contains(ni.netId.Value)) fresh.Add(post[i]); }
                string res = Cost(fresh, pos);
                for (int i = 0; i < fresh.Count; i++)
                {
                    try { if (fresh[i] != null) ub.CmdDestroyBluePrint(fresh[i].gameObject); } catch (Exception e) { BtcLog.Warn("EcoBp", "delete: " + e.Message); }
                }
                for (int f = 0; f < 5; f++) yield return null;
                yield return "bpcost|" + key + "|" + res;
                yield break;
            }
            yield return "bpcost|" + key + "|FAILED no free spot|";
        }

        private static string Cost(List<Blueprint> bps, Vector3 origin)
        {
            Dictionary<string, int> tot = new Dictionary<string, int>();
            StringBuilder fp = new StringBuilder();
            HashSet<string> cells = new HashSet<string>();
            XYZ o = XYZ.FromVector3(origin);
            for (int i = 0; i < bps.Count; i++)
            {
                Blueprint b = bps[i];
                if (b == null) continue;
                try
                {
                    Recepteur r = b.recepteur;
                    Description d = r != null ? r.BaseCapacity : null;
                    if (d != null && d.DicoAdjectif != null)
                        foreach (KeyValuePair<Type, Adjectif> kv in d.DicoAdjectif)
                        {
                            Quantite q = kv.Value as Quantite;
                            if (q == null) continue;
                            string nm = kv.Value.GetKey().codeName;
                            int c; tot.TryGetValue(nm, out c); tot[nm] = c + (int)q.valeur;
                        }
                }
                catch (Exception e) { BtcLog.Warn("EcoBp", "cost: " + e.Message); }
                try
                {
                    foreach (KeyValuePair<XYZ, VPropertyMask> kv in (IEnumerable<KeyValuePair<XYZ, VPropertyMask>>)b.volume)
                    {
                        if ((kv.Value & VPropertyMask.fantome) == 0) continue;
                        string c = (kv.Key.x - o.x) + "," + (kv.Key.y - o.y) + "," + (kv.Key.z - o.z);
                        if (cells.Add(c)) { if (fp.Length > 0) fp.Append(','); fp.Append(c); }
                    }
                }
                catch { }
            }
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in tot) { if (sb.Length > 0) sb.Append(','); sb.Append(kv.Key).Append('*').Append(kv.Value); }
            return sb + "|" + fp + "|blueprints=" + bps.Count;
        }

        // ------------------------------------------------------------ ecoplay
        internal static void Play(string[] p)
        {
            try { Inst().PlayStart(p.Length > 1 ? p[1] : null); }
            catch (Exception e) { BtcLog.Error("EcoPlay", "failed: " + e); }
        }

        private void PlayStart(string path)
        {
            if (path == null) { BtcLog.Warn("EcoPlay", "usage: ecoplay <path>"); return; }
            if (!File.Exists(path)) { string alt = System.IO.Path.Combine(BtcDevPaths.LogDir, path); if (File.Exists(alt)) path = alt; }
            string[] lines = File.ReadAllLines(path);
            _play.Clear(); _map.Clear(); _traceBp.Clear(); _bpNext = 0; _bpSeen.Clear(); _pi = 0; _playOk = 0; _playFail = 0; _unsup.Clear();
            List<long> unitIds = new List<long>();
            List<uint> placeIds = new List<uint>(); List<Vector3> placeCells = new List<Vector3>();
            List<long> placeTraceIds = new List<long>();
            for (int i = 0; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(new[] { '|' }, 3);
                if (f.Length < 3) continue;
                float t = F(f[0]);
                if (f[1] == "PLACE")
                {
                    string[] a = f[2].Split('|');
                    long id; if (a.Length < 2 || !long.TryParse(a[0], out id)) continue;
                    string[] v = a[1].Split(',');
                    if (v.Length < 3) continue;
                    placeTraceIds.Add(id); placeCells.Add(new Vector3(F(v[0]), F(v[1]), F(v[2])));
                    if (!unitIds.Contains(id)) unitIds.Add(id);
                }
                else if (f[1] == "CMD") { PlayLine pl = new PlayLine(); pl.T = t; pl.Raw = f[2]; pl.Kind = "CMD"; _play.Add(pl); }
                else if (f[1] == "AUTO" || f[1] == "INV") { string[] a = f[2].Split('|'); long id; if (long.TryParse(a[0], out id) && !unitIds.Contains(id)) unitIds.Add(id); }
                else if (f[1] == "BLD") { string[] a = f[2].Split('|'); long id; if (long.TryParse(a[0], out id) && !_traceBp.Contains(id)) _traceBp.Add(id); }
            }
            unitIds.Sort();
            List<Labor> us = Units();
            for (int i = 0; i < unitIds.Count && i < us.Count; i++) _map[unitIds[i]] = IdOf(us[i]);
            BtcLog.Info("EcoPlay", "loaded " + _play.Count + " CMD, " + unitIds.Count + " trace units -> " + us.Count + " live, place " + placeCells.Count + ", bp ids " + _traceBp.Count);
            if (placeCells.Count > 0)
            {
                List<uint> live = new List<uint>();
                for (int i = 0; i < placeTraceIds.Count; i++) { uint lid; live.Add(_map.TryGetValue(placeTraceIds[i], out lid) ? lid : (uint)placeTraceIds[i]); }
                ApplyPlace(live, placeCells);
            }
            foreach (Blueprint b in UnityEngine.Object.FindObjectsOfType<Blueprint>()) _bpSeen.Add(IdOf(b));
            _pt0 = Time.time; _playing = true; Busy = true;
        }

        private void PlayTick()
        {
            if (_placeBusy) { _pt0 = Time.time; return; } // time stands still during placement
            float now = Time.time - _pt0;
            while (_pi < _play.Count && _play[_pi].T <= now)
            {
                try { Exec(_play[_pi].Raw); } catch (Exception e) { _playFail++; BtcLog.Warn("EcoPlay", "exec: " + e.Message); }
                _pi++;
            }
            if (_pi >= _play.Count)
            {
                _playing = false; Busy = false;
                StringBuilder s = new StringBuilder();
                foreach (KeyValuePair<string, int> kv in _unsup) s.Append(kv.Key).Append('x').Append(kv.Value).Append(' ');
                BtcLog.Info("EcoPlay", "done ok=" + _playOk + " failed=" + _playFail + " unsupported: " + s);
            }
        }

        private GameObject Resolve(long traceId)
        {
            if (traceId <= 0) return null;
            uint live;
            if (!_map.TryGetValue(traceId, out live)) live = (uint)traceId;
            NetworkIdentity ni;
            if (NetworkServer.objects.TryGetValue(new NetworkInstanceId(live), out ni) && ni != null) return ni.gameObject;
            return null;
        }

        private void MapNewBlueprints()
        {
            Blueprint[] all = UnityEngine.Object.FindObjectsOfType<Blueprint>();
            List<uint> fresh = new List<uint>();
            for (int i = 0; i < all.Length; i++) { uint id = IdOf(all[i]); if (_bpSeen.Add(id)) fresh.Add(id); }
            fresh.Sort();
            for (int i = 0; i < fresh.Count && _bpNext < _traceBp.Count; i++) _map[_traceBp[_bpNext++]] = fresh[i];
        }

        private void Exec(string body)
        {
            string[] f = body.Split('|');
            string name = f[0];
            long actor = f.Length > 1 && f[1].Length > 0 ? long.Parse(f[1], Inv) : -1;
            long target = f.Length > 2 && f[2].Length > 0 ? long.Parse(f[2], Inv) : -1;
            int[] vox = new int[3]; bool haveVox = false;
            if (f.Length > 3 && f[3].Length > 0) { string[] v = f[3].Split(','); if (v.Length == 3) { vox[0] = (int)F(v[0]); vox[1] = (int)F(v[1]); vox[2] = (int)F(v[2]); haveVox = true; } }
            int[] pp = new int[4];
            if (f.Length > 4 && f[4].Length > 0) { string[] v = f[4].Split(','); for (int i = 0; i < 4 && i < v.Length; i++) pp[i] = (int)F(v[i]); }
            float[] ff = new float[2];
            if (f.Length > 5 && f[5].Length > 0) { string[] v = f[5].Split(','); for (int i = 0; i < 2 && i < v.Length; i++) ff[i] = F(v[i]); }
            string us = null, vs = null, ss = null, qs = null;
            for (int i = 6; i < f.Length; i++)
            {
                if (f[i].StartsWith("u=")) us = f[i].Substring(2);
                else if (f[i].StartsWith("v=")) vs = f[i].Substring(2);
                else if (f[i].StartsWith("s=")) ss = f[i].Substring(2);
                else if (f[i].StartsWith("q=")) qs = f[i].Substring(2);
            }
            MapNewBlueprints();
            MethodInfo mi = null; object owner = null;
            Find(name, out mi, out owner);
            if (mi == null) { Unsup(name + "(no method)"); return; }
            ParameterInfo[] ps = mi.GetParameters();
            object[] args = new object[ps.Length];
            int iGo = 0, iInt = 0, iF = 0, iV = 0, iS = 0, iQ = 0;
            string[] vl = vs != null ? vs.Split(';') : new string[0];
            string[] sl = ss != null ? ss.Split(';') : new string[0];
            string[] ql = qs != null ? qs.Split(';') : new string[0];
            for (int i = 0; i < ps.Length; i++)
            {
                Type t = ps[i].ParameterType;
                if (t == typeof(GameObject) || typeof(Component).IsAssignableFrom(t))
                {
                    long id = iGo == 0 ? actor : target; iGo++;
                    GameObject g = Resolve(id);
                    if (g == null && id > 0) { Unsup(name + "(unresolved id)"); return; }
                    args[i] = g == null ? null : (t == typeof(GameObject) ? (object)g : g.GetComponent(t));
                }
                else if (t == typeof(Vector3))
                {
                    if (iV < vl.Length) { string[] c = vl[iV++].Split(','); args[i] = new Vector3(F(c[0]), F(c[1]), F(c[2])); }
                    else args[i] = new Vector3(vox[0] + 0.5f, vox[1], vox[2] + 0.5f);
                }
                else if (t == typeof(XYZ)) args[i] = new XYZ(vox[0], vox[1], vox[2]);
                else if (t == typeof(Quaternion)) args[i] = Quaternion.Euler(0f, iQ < ql.Length ? F(ql[iQ++]) : 0f, 0f);
                else if (t == typeof(string)) args[i] = iS < sl.Length ? sl[iS++] : "";
                else if (t == typeof(bool)) args[i] = (iInt < 4 ? pp[iInt++] : 0) != 0;
                else if (t == typeof(int)) args[i] = iInt < 4 ? pp[iInt++] : 0;
                else if (t == typeof(uint)) args[i] = (uint)(iInt < 4 ? pp[iInt++] : 0);
                else if (t == typeof(short)) args[i] = (short)(iInt < 4 ? pp[iInt++] : 0);
                else if (t == typeof(byte)) args[i] = (byte)(iInt < 4 ? pp[iInt++] : 0);
                else if (t.IsEnum) args[i] = Enum.ToObject(t, iInt < 4 ? pp[iInt++] : 0);
                else if (t == typeof(float)) args[i] = iF < 2 ? ff[iF++] : 0f;
                else if (t == typeof(double)) args[i] = (double)(iF < 2 ? ff[iF++] : 0f);
                else if (t == typeof(GameObject[]) || typeof(IEnumerable).IsAssignableFrom(t))
                {
                    List<GameObject> gl = new List<GameObject>();
                    if (us != null)
                        foreach (string s in us.Split(';')) { long id; if (long.TryParse(s, out id)) { GameObject g = Resolve(id); if (g != null) gl.Add(g); } }
                    if (t == typeof(GameObject[])) args[i] = gl.ToArray();
                    else if (t.IsAssignableFrom(typeof(List<GameObject>))) args[i] = gl;
                    else { Unsup(name + "(param " + t.Name + ")"); return; }
                }
                else { Unsup(name + "(param " + t.Name + ")"); return; }
            }
            mi.Invoke(owner, args);
            _playOk++;
        }

        private void Unsup(string what)
        {
            int c; _unsup.TryGetValue(what, out c); _unsup[what] = c + 1;
            _playFail++;
            if (c == 0) BtcLog.Warn("EcoPlay", "unsupported: " + what);
        }

        private static readonly Dictionary<string, KeyValuePair<MethodInfo, Type>> Cache = new Dictionary<string, KeyValuePair<MethodInfo, Type>>();

        private static void Find(string name, out MethodInfo mi, out object owner)
        {
            mi = null; owner = null;
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            NetworkBehaviour[] nb = User.LocalUser.GetComponents<NetworkBehaviour>();
            for (int i = 0; i < nb.Length; i++)
            {
                MethodInfo m = nb[i].GetType().GetMethod(name, bf);
                if (m != null) { mi = m; owner = nb[i]; return; }
            }
            NetworkBehaviour[] all = UnityEngine.Object.FindObjectsOfType<NetworkBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                MethodInfo m = all[i].GetType().GetMethod(name, bf);
                if (m != null) { mi = m; owner = all[i]; return; }
            }
        }
    }

    /// <summary>DEV: while BtcDevEcoOps.NoOre is set, deep ore types (iron/crystals) are generated as Stone (survives voxel regeneration).</summary>
    internal static class BtcDevNoOrePatch
    {
        internal static void Postfix(ref VoxelTerrainType __result)
        {
            if (!BtcDevEcoOps.NoOre) return;
            int t = (int)__result;
            if (t == 54 || t == 44 || t == 38 || t == 55 || t == 57 || t == 59) __result = (VoxelTerrainType)8;
        }
    }
}
#endif
