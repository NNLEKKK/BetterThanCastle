#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Assets;
using Brix.Engine;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using Brix.Game.Semantique;
using Brix.Legacy;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV: "ecotrace 1|0". Event log BTClogs/eco-yyyyMMdd-HHmmss.txt, lines "t|TAG|msg" (docs/eco/DESIGN.md "eco trace").
    /// CMD comes from UNetCmdReplay.Run (single hook for all UNet commands). Everything else is a 5 Hz diff poll of the world
    /// (allocations are accepted here: this is a DEV recorder). Resource names are logged once as "NOTE|res|name" for milestone tuning.
    /// </summary>
    internal sealed class BtcDevEcoTrace : MonoBehaviour
    {
        internal static bool On;
        private static BtcDevEcoTrace _inst;
        private static StreamWriter _w;
        private static float _t0;
        private static bool _hooked;
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        private static readonly List<XYZ> VoxQ = new List<XYZ>();
        private static readonly Dictionary<long, int> VoxOld = new Dictionary<long, int>();

        private float _next;
        private float _nextTree;
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly Dictionary<uint, string> _inv = new Dictionary<uint, string>();
        private readonly Dictionary<uint, int> _auto = new Dictionary<uint, int>();
        private readonly Dictionary<uint, string> _stock= new Dictionary<uint, string>();
        private readonly Dictionary<uint, Task> _task = new Dictionary<uint, Task>();
        private readonly Dictionary<uint, string> _craft = new Dictionary<uint, string>();
        private readonly HashSet<uint> _bld = new HashSet<uint>();
        private readonly HashSet<string> _resSeen = new HashSet<string>();
        private readonly HashSet<string> _mile = new HashSet<string>();
        private readonly Dictionary<string, int> _base = new Dictionary<string, int>();
        private bool _baseSet;
        private int _treeBase = -1;
        private int _kitMax, _wardMax;
        private static FieldInfo _fWork, _fHeat, _fFinished;

        internal static void Set(bool on)
        {
            if (on == On) return;
            if (on) Begin(); else End("stop");
        }

        private static void Begin()
        {
            try
            {
                Directory.CreateDirectory(BtcDevPaths.LogDir);
                string path = System.IO.Path.Combine(BtcDevPaths.LogDir, "eco-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                _w = new StreamWriter(path, false, new UTF8Encoding(false));
                _t0 = Time.time;
                On = true;
                string gp = "?", map = "?";
                try { gp = BtcGameplay.Encode(BtcGameplay.Active); } catch { }
                try { if (GameParam.Map != null) map = GameParam.Map.displayName; } catch { }
                Line("CFG", "map=" + map + ";gp=" + gp);
                try
                {
                    StringBuilder an = new StringBuilder("autonomy");
                    foreach (AutonomyStatus.Ids ai in Enum.GetValues(typeof(AutonomyStatus.Ids))) an.Append('|').Append((int)ai).Append('=').Append(ai);
                    Line("NOTE", an.ToString());
                }
                catch { }
                if (!_hooked)
                {
                    _hooked = true;
                    GameSignals.Connect<XYZ>(GameSignals.TerrainChanged, delegate (XYZ p) { QueueVox(p); }, null);
                    GameSignals.Connect<XYZ>(GameSignals.TerrainRemoved, delegate (XYZ p) { QueueVox(p); }, null);
                    Type rt = typeof(Recipe);
                    BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
                    _fWork = rt.GetField("work", bf); _fHeat = rt.GetField("heat", bf); _fFinished = rt.GetField("finished", bf);
                }
                VoxQ.Clear(); VoxOld.Clear();
                if (_inst == null)
                {
                    GameObject go = new GameObject("BtcDevEcoTrace");
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _inst = go.AddComponent<BtcDevEcoTrace>();
                }
                _inst.Reset();
                BtcLog.Info("EcoTrace", "on: " + path);
            }
            catch (Exception e) { On = false; BtcLog.Error("EcoTrace", "begin failed: " + e); }
        }

        private static void End(string reason)
        {
            if (!On) return;
            try { Line("END", reason); _w.Flush(); _w.Dispose(); } catch { }
            _w = null; On = false;
            BtcLog.Info("EcoTrace", "off: " + reason);
        }

        private void OnDestroy() { if (On) End("destroyed"); }

        private void Reset()
        {
            _next = 0f; _nextTree = 0f; _baseSet = false; _treeBase = -1; _kitMax = 0; _wardMax = 0;
            _auto.Clear(); _inv.Clear(); _stock.Clear(); _task.Clear(); _craft.Clear(); _bld.Clear(); _mile.Clear(); _base.Clear(); _resSeen.Clear();
        }

        private static void QueueVox(XYZ p)
        {
            if (!On) return;
            if (VoxQ.Count < 5000) VoxQ.Add(p);
        }

        internal static int Mute;
        internal static void Line(string tag, string msg)
        {
            if (_w == null) return;
            _w.Write((Time.time - _t0).ToString("0.000", Inv)); _w.Write('|'); _w.Write(tag); _w.Write('|'); _w.WriteLine(msg);
        }

        // ------------- CMD -------------
        /// <summary>Called from UNetCmdReplay.Run (every server-side [Command] body of the replay-hooked UNet classes).</summary>
        internal static void Cmd(string name, object[] args)
        {
            if (!On || Mute > 0) return;
            try
            {
                // Format (lab EcoCommandText): Name|actor|target|x,y,z|p0,p1,p2,p3|f0,f1 [|u=id;id..][|v=raw vec][|s=text]
                StringBuilder sb = new StringBuilder(96);
                long actor = -1, target = -1;
                bool haveVox = false; int vx = 0, vy = 0, vz = 0;
                int[] pp = new int[4]; int np = 0;
                float[] ff = new float[2]; int nf = 0;
                StringBuilder units = null, raw = null, strs = null, qs = null;
                if (args != null)
                    for (int i = 0; i < args.Length; i++)
                    {
                        object o = args[i];
                        if (o == null) continue;
                        if (o is GameObject || o is Component || o is NetworkIdentity)
                        {
                            long id = IdOf(o);
                            if (actor < 0) actor = id; else if (target < 0) target = id;
                        }
                        else if (o is string) { if (strs == null) strs = new StringBuilder(); if (strs.Length > 0) strs.Append(';'); strs.Append((string)o); }
                        else if (o is Vector3)
                        {
                            Vector3 v = (Vector3)o;
                            if (!haveVox) { haveVox = true; vx = Mathf.FloorToInt(v.x); vy = Mathf.FloorToInt(v.y); vz = Mathf.FloorToInt(v.z); }
                            if (raw == null) raw = new StringBuilder(); if (raw.Length > 0) raw.Append(';'); raw.Append(F3(v.x)).Append(',').Append(F3(v.y)).Append(',').Append(F3(v.z));
                        }
                        else if (o is Quaternion) { if (qs == null) qs = new StringBuilder(); if (qs.Length > 0) qs.Append(';'); qs.Append(F3(((Quaternion)o).eulerAngles.y)); }
                        else if (o is XYZ) { XYZ v = (XYZ)o; if (!haveVox) { haveVox = true; vx = v.x; vy = v.y; vz = v.z; } }
                        else if (o is float) { if (nf < 2) ff[nf++] = (float)o; }
                        else if (o is double) { if (nf < 2) ff[nf++] = (float)(double)o; }
                        else if (o is bool) { if (np < 4) pp[np++] = (bool)o ? 1 : 0; }
                        else if (o is int || o is uint || o is short || o is byte || o is long || o is Enum) { if (np < 4) pp[np++] = Convert.ToInt32(o is Enum ? Convert.ChangeType(o, Enum.GetUnderlyingType(o.GetType())) : o, Inv); }
                        else if (o is IEnumerable)
                        {
                            foreach (object e in (IEnumerable)o)
                            {
                                if (e is GameObject || e is Component || e is NetworkIdentity)
                                {
                                    long id = IdOf(e);
                                    if (units == null) units = new StringBuilder();
                                    if (units.Length > 0) units.Append(';'); units.Append(id);
                                }
                            }
                            if (units != null && actor < 0) actor = long.Parse(units.ToString().Split(';')[0], Inv);
                        }
                    }
                sb.Append(name).Append('|').Append(actor).Append('|').Append(target).Append('|');
                if (haveVox) sb.Append(vx).Append(',').Append(vy).Append(',').Append(vz);
                sb.Append('|').Append(pp[0]).Append(',').Append(pp[1]).Append(',').Append(pp[2]).Append(',').Append(pp[3]);
                sb.Append('|').Append(F3(ff[0])).Append(',').Append(F3(ff[1]));
                if (units != null) sb.Append("|u=").Append(units);
                if (raw != null) sb.Append("|v=").Append(raw);
                if (strs != null) sb.Append("|s=").Append(strs);
                if (qs != null) sb.Append("|q=").Append(qs);
                Line("CMD", sb.ToString());
            }
            catch (Exception e) { BtcLog.Warn("EcoTrace", "cmd fmt " + name + ": " + e.Message); }
        }

        private static long IdOf(object o)
        {
            NetworkIdentity ni = o as NetworkIdentity;
            if (ni == null)
            {
                GameObject go = o as GameObject;
                if (go == null) { Component c = o as Component; if (c != null) go = c.gameObject; }
                if (go != null) ni = go.GetComponentInParent<NetworkIdentity>();
            }
            return ni != null ? (long)ni.netId.Value : -1L;
        }

        private static string F3(float f) { return f.ToString("0.###", Inv); }

        private static void Fmt(StringBuilder sb, object o, int depth)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string) { sb.Append((string)o); return; }
            if (o is bool) { sb.Append((bool)o ? 1 : 0); return; }
            if (o is float) { sb.Append(F3((float)o)); return; }
            if (o is double) { sb.Append(((double)o).ToString("0.###", Inv)); return; }
            if (o is Vector3) { Vector3 v = (Vector3)o; sb.Append(F3(v.x)).Append(',').Append(F3(v.y)).Append(',').Append(F3(v.z)); return; }
            if (o is Quaternion) { sb.Append(F3(((Quaternion)o).eulerAngles.y)); return; }
            if (o is XYZ) { XYZ v = (XYZ)o; sb.Append(v.x).Append(',').Append(v.y).Append(',').Append(v.z); return; }
            if (o is NetworkIdentity) { sb.Append(((NetworkIdentity)o).netId.Value); return; }
            if (o is GameObject || o is Component)
            {
                GameObject go = o is GameObject ? (GameObject)o : ((Component)o).gameObject;
                NetworkIdentity ni = go != null ? go.GetComponentInParent<NetworkIdentity>() : null;
                sb.Append(ni != null ? ni.netId.Value.ToString() : (go != null ? "go:" + go.name : "null"));
                return;
            }
            if (o is NetworkInstanceId) { sb.Append(((NetworkInstanceId)o).Value); return; }
            if (o is IEnumerable && depth < 2)
            {
                sb.Append('[');
                bool first = true;
                foreach (object e in (IEnumerable)o) { if (!first) sb.Append(';'); first = false; Fmt(sb, e, depth + 1); }
                sb.Append(']');
                return;
            }
            sb.Append(Convert.ToString(o, Inv));
        }

        // ------------- poll -------------
        private void Update()
        {
            if (!On) return;
            if (Time.time < _next) return;
            _next = Time.time + 0.2f;
            try { Poll(); }
            catch (Exception e) { BtcLog.Warn("EcoTrace", "poll: " + e.Message + " @ " + (e.StackTrace ?? "").Split((char)10)[0]); }
            try { _w.Flush(); } catch { }
        }

        private static string Content(Recepteur r)
        {
            return r == null ? "" : BtcDevEcoDump.Desc(r.ContentDescription);
        }

        private static int CountIn(Description d, string sub)
        {
            int n = 0;
            if (d == null || d.DicoAdjectif == null) return 0;
            foreach (KeyValuePair<Type, Adjectif> kv in d.DicoAdjectif)
            {
                Adjectif a = kv.Value; if (a == null) continue;
                if (a.GetKey().codeName.IndexOf(sub, StringComparison.OrdinalIgnoreCase) < 0) continue;
                Quantite q = a as Quantite; n += q != null ? q.valeur : 1;
            }
            return n;
        }

        private void Mile(string id)
        {
            if (_mile.Add(id)) Line("MILE", id);
        }

        private void Poll()
        {
            NetworkIdentity[] all = UnityEngine.Object.FindObjectsOfType<NetworkIdentity>();
            Dictionary<string, int> cnt = new Dictionary<string, int>();
            _sb.Length = 0;
            HashSet<uint> bldNow = new HashSet<uint>();
            int poleRack = 0, kettleRack = 0, wardCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                NetworkIdentity ni = all[i];
                if (ni == null) continue;
                GameObject go = ni.gameObject;
                uint id = ni.netId.Value;
                Labor lab = go.GetComponent<Labor>();
                if (lab != null)
                {
                    Vector3 p = go.transform.position;
                    _sb.Append(id).Append(':').Append(F3(p.x)).Append(',').Append(F3(p.y)).Append(',').Append(F3(p.z)).Append(';');
                    int au = -1;
                    try { au = (int)lab.Autonomy.Id; } catch { }
                    int auOld;
                    if (!_auto.TryGetValue(id, out auOld) || auOld != au) { _auto[id] = au; Line("AUTO", id + "|" + au); }
                    string inv = Content(lab.recepteur);
                    string old;
                    if (!_inv.TryGetValue(id, out old)) old = "";
                    if (inv != old) { _inv[id] = inv; Line("INV", id + "|" + inv); }
                    Task cur = lab.CurrentTask;
                    Task prev;
                    _task.TryGetValue(id, out prev);
                    if (cur != prev)
                    {
                        if (prev != null) Line("TASK", id + "|end|" + (prev.Description ?? "-"));
                        if (cur != null) { Fmt1(cur, id); }
                        _task[id] = cur;
                    }
                    continue;
                }
                Blueprint bp = go.GetComponent<Blueprint>();
                if (bp != null)
                {
                    bldNow.Add(id);
                    if (_bld.Add(id)) Line("BLD", id + "|0/1|" + BtcDevEcoDump.K(bp.BlueprintResult));
                    continue;
                }
                string kindKey = null;
                CraftingStation cs = go.GetComponent<CraftingStation>();
                ToolRack rack = go.GetComponent<ToolRack>();
                Recepteur rc = go.GetComponent<Recepteur>();
                if (cs != null)
                {
                    string tn = cs.GetType().Name;
                    Inc(cnt, "st:" + tn);
                    kindKey = tn;
                    PollCraft(cs, id);
                }
                else if (rack != null)
                {
                    Inc(cnt, "st:rack");
                    if (rc != null) { poleRack += CountIn(rc.ContentDescription, "poleaxe") + CountIn(rc.ContentDescription, "halberd"); kettleRack += CountIn(rc.ContentDescription, "kettle"); }
                }
                else if (go.GetComponent<PassThroughStockpile>() != null || go.GetComponent<ResourceConverter>() != null) Inc(cnt, "st:stockpile");
                else
                {
                    GameComponent gc = go.GetComponent<GameComponent>();
                    if (gc != null) { string nm = BtcDevEcoDump.GK(gc).ToLowerInvariant(); if (nm.Contains("chop")) Inc(cnt, "st:chop"); }
                }
                if (rc != null && (cs != null || rack != null || kindKey == null))
                {
                    if (cs != null || rack != null || go.GetComponent<PassThroughStockpile>() != null || go.GetComponent<ResourceConverter>() != null)
                    {
                        string c = Content(rc), old;
                        if (!_stock.TryGetValue(id, out old)) old = "";
                        if (c != old) { _stock[id] = c; Line("STOCK", id + "|" + c); }
                    }
                }
                Apparence ap = go.GetComponent<Apparence>();
                if (ap != null && ap.Principale != null && rc == null)
                {
                    string res = ap.Principale.GetKey().codeName;
                    if (_resSeen.Add(res)) Line("NOTE", "res|" + res);
                    Inc(cnt, "res:" + res.ToLowerInvariant());
                    if (res.IndexOf("ward", StringComparison.OrdinalIgnoreCase) >= 0 && res.IndexOf("capture", StringComparison.OrdinalIgnoreCase) >= 0) wardCount++;
                }
            }
            // BLD done
            List<uint> gone = null;
            foreach (uint b in _bld) if (!bldNow.Contains(b)) { if (gone == null) gone = new List<uint>(); gone.Add(b); }
            if (gone != null) for (int i = 0; i < gone.Count; i++) { _bld.Remove(gone[i]); Line("BLD", gone[i] + "|1/1|done"); }
            if (_sb.Length > 0) Line("SNAP", _sb.ToString());
            DrainVox();

            // trees at 1 Hz
            int trees = _treeBase;
            if (Time.time >= _nextTree)
            {
                _nextTree = Time.time + 1f;
                try
                {
                    TreeRegistry tr = TreeRegistry.Instance;
                    if (tr != null) { int n = 0; foreach (XYZ _ in tr.ListAllTreePosition()) n++; trees = n; }
                }
                catch { }
            }
            if (!_baseSet)
            {
                _baseSet = true;
                foreach (KeyValuePair<string, int> kv in cnt) _base[kv.Key] = kv.Value;
                _treeBase = trees;
                _wardMax = wardCount;
                return;
            }
            if (_treeBase >= 0 && trees >= 0 && trees < _treeBase) Mile("tree_chopped");
            if (trees > _treeBase) _treeBase = trees;
            if (Above(cnt, "st:chop")) Mile("chopping_block");
            if (Above(cnt, "st:stockpile")) Mile("stockpile");
            if (Above(cnt, "st:Furnace")) Mile("furnace");
            if (Above(cnt, "st:rack")) Mile("weapon_rack");
            if (Above(cnt, "st:Workbench")) Mile("workbench");
            if (Above(cnt, "st:Altar")) Mile("altar");
            if (Any(cnt, "res:", "plank")) Mile("planks");
            if (Any(cnt, "res:", "stoneblock") || Any(cnt, "res:", "brick")) Mile("bricks");
            if (Any(cnt, "res:", "ingot")) Mile("ingot");
            if (Any(cnt, "res:", "poleaxe") || Any(cnt, "res:", "halberd")) Mile("halberd");
            if (Any(cnt, "res:", "kettle")) Mile("kettlehat");
            if (wardCount > 0) Mile("capture_ward");
            int kits = Math.Min(poleRack, kettleRack);
            while (_kitMax < kits && _kitMax < 8) { _kitMax++; Mile("kit_" + _kitMax); }
            while (_wardMax < wardCount && _wardMax < 2) { _wardMax++; Mile("ward_" + _wardMax); }
        }

        private void Fmt1(Task cur, uint id)
        {
            string tgt = "-";
            try { if (cur.Giver != null) { NetworkIdentity gi = cur.Giver.GetComponentInParent<NetworkIdentity>(); tgt = gi != null ? gi.netId.Value.ToString() : cur.Giver.name; } } catch { }
            Line("TASK", id + "|start|" + (cur.Description ?? "-") + "|" + tgt);
        }

        private void PollCraft(CraftingStation cs, uint id)
        {
            Recipe r = null;
            try { r = cs.CurrentRecipe; } catch { }
            string cur = "", old;
            if (r != null && r.Info != null)
            {
                string w = _fWork != null ? Convert.ToString(_fWork.GetValue(r), Inv) : "?";
                string h = _fHeat != null ? Convert.ToString(_fHeat.GetValue(r), Inv) : "?";
                cur = r.Info.id + "|work=" + w + "|heat=" + h;
            }
            if (!_craft.TryGetValue(id, out old)) old = "";
            if (cur == old) return;
            if (cur.Length == 0 && old.Length > 0) Line("CRAFT", id + "|" + old.Substring(0, old.IndexOf('|')) + "|done");
            else if (cur.Length > 0) Line("CRAFT", id + "|" + cur);
            _craft[id] = cur;
        }

        private static void Inc(Dictionary<string, int> d, string k)
        {
            int n; d.TryGetValue(k, out n); d[k] = n + 1;
        }

        private bool Above(Dictionary<string, int> cnt, string k)
        {
            int n, b; cnt.TryGetValue(k, out n); _base.TryGetValue(k, out b);
            return n > b;
        }

        private bool Any(Dictionary<string, int> cnt, string prefix, string sub)
        {
            foreach (KeyValuePair<string, int> kv in cnt)
                if (kv.Value > 0 && kv.Key.StartsWith(prefix, StringComparison.Ordinal) && kv.Key.IndexOf(sub, StringComparison.Ordinal) >= 0)
                {
                    int b; _base.TryGetValue(kv.Key, out b);
                    if (kv.Value > b) return true;
                }
            return false;
        }

        private static int TypeAt(XYZ p)
        {
            try
            {
                if (!Voxel.IsNonEmpty(p)) return 0;
                using (IVoxel v = Voxel.GetVoxel(p)) { return v == null || v.IsNull || v.BlockInfo == null ? 0 : (v.IsTerrain ? v.BlockInfo.ID : -v.BlockInfo.ID); }
            }
            catch { return 0; }
        }

        private static void DrainVox()
        {
            if (VoxQ.Count == 0) return;
            HashSet<long> done = new HashSet<long>();
            for (int i = 0; i < VoxQ.Count; i++)
            {
                XYZ p = VoxQ[i];
                long key = ((long)(p.x + 100000) << 40) ^ ((long)(p.y + 1000) << 20) ^ (long)(p.z + 100000);
                if (!done.Add(key)) continue;
                int nw = TypeAt(p), old;
                bool had = VoxOld.TryGetValue(key, out old);
                VoxOld[key] = nw;
                Line("VOX", p.x + "," + p.y + "," + p.z + "|" + (had ? old.ToString() : "?") + "|" + nw);
            }
            VoxQ.Clear();
        }
    }
}
#endif
