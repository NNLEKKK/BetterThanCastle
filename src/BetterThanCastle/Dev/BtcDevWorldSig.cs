#if DEV
using System;
using System.Collections.Generic;
using System.Text;
using Brix.Components;
using Brix.Engine;
using Brix.Engine.Blocks;
using Brix.Legacy;
using Brix.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV only. Replay seek fidelity: a world signature beyond the network payloads (what the eye sees): object kinds, parenting,
    /// block engine roots, trees, full-scan terrain. "worldsig tag" logs it and keeps it under tag; "worldsig tag ref" also diffs vs ref.
    /// </summary>
    internal static class BtcDevWorldSig
    {
        private sealed class Sig
        {
            public Dictionary<string, int> Kinds = new Dictionary<string, int>();
            public Dictionary<string, HashSet<uint>> Ids = new Dictionary<string, HashSet<uint>>();
            public Dictionary<uint, string> Parent = new Dictionary<uint, string>();
            public HashSet<string> Blocks = new HashSet<string>();
            public HashSet<string> Trees = new HashSet<string>();
            public HashSet<string> Renders = new HashSet<string>();
            public HashSet<string> Wards = new HashSet<string>();
            public HashSet<string> Crystals = new HashSet<string>();
            public int TerrainLen;
            public uint TerrainHash;
            public byte[] Terrain;
        }

        private static readonly Dictionary<string, Sig> Saved = new Dictionary<string, Sig>();

        internal static void Run(string tag, string refTag)
        {
            Sig s = new Sig();
            foreach (KeyValuePair<NetworkInstanceId, NetworkIdentity> kv in ClientScene.objects)
            {
                NetworkIdentity ni = kv.Value;
                if (ni == null) continue;
                if (BtcReplaySeek.IsGhost(kv.Key.Value, ni)) { Inc(s.Kinds, "~stale"); continue; }
                string k = Kind(ni.gameObject.name) + (ni.gameObject.activeSelf ? "" : "#off");
                Inc(s.Kinds, k);
                HashSet<uint> hs; if (!s.Ids.TryGetValue(k, out hs)) { hs = new HashSet<uint>(); s.Ids[k] = hs; } hs.Add(kv.Key.Value);
                Transform p = ni.transform.parent;
                NetworkIdentity pi = p != null ? p.GetComponentInParent<NetworkIdentity>() : null;
                s.Parent[kv.Key.Value] = (pi != null ? pi.netId.Value.ToString() : (p != null ? "t:" + p.name : "-")) + (ni.gameObject.activeSelf ? "" : "#off");
                // what is visibly attached to a unit (kits, hats, carried things), network object or not
                if (ni.GetComponent<Brix.Game.AI.Labor>() != null)
                {
                    Renderer[] rs = ni.GetComponentsInChildren<Renderer>(false);
                    for (int i = 0; i < rs.Length; i++) if (rs[i].enabled) s.Renders.Add(kv.Key.Value + ":" + Kind(rs[i].gameObject.name));
                }
            }
            try
            {
                foreach (RootBlockData rb in BrixSingleton<BlockEngine>.Instance.RootBlocks)
                    s.Blocks.Add(rb.Position.x + "," + rb.Position.y + "," + rb.Position.z);
            }
            catch (Exception e) { BtcLog.Warn("WorldSig", "blocks: " + e.Message); }
            try
            {
                foreach (XYZ t in TreeRegistry.Instance.ListAllTreePosition()) s.Trees.Add(t.x + "," + t.y + "," + t.z);
            }
            catch (Exception e) { BtcLog.Warn("WorldSig", "trees: " + e.Message); }
            // orphans: active networked objects UNET no longer lists (never destroyed by a later seek: user "ghost workers/wards")
            try
            {
                Dictionary<string, int> orph = new Dictionary<string, int>();
                foreach (NetworkIdentity ni in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
                {
                    if (ni.netId.IsEmpty()) continue;
                    GameObject reg = ClientScene.FindLocalObject(ni.netId);
                    if (reg == ni.gameObject) continue;
                    Inc(orph, Kind(ni.gameObject.name) + (reg == null ? "" : "/dup"));
                }
                foreach (KeyValuePair<string, int> kv in orph) Inc(s.Kinds, "ORPHAN " + kv.Key + " x" + kv.Value);
                if (orph.Count > 0) { string o = ""; foreach (KeyValuePair<string, int> kv in orph) o += kv.Key + " " + kv.Value + ", "; BtcLog.Info("WorldSig", tag + " ORPHANS: " + o); }
            }
            catch (Exception e) { BtcLog.Warn("WorldSig", "orphans: " + e.Message); }
            try { Visuals(s); }
            catch (Exception e) { BtcLog.Warn("WorldSig", "visuals: " + e.Message); }
            try
            {
                byte[] tb = BtcReplayAnchor.TerrainBlobFull();
                s.TerrainLen = tb != null ? tb.Length : -1;
                uint h = 2166136261u;
                if (tb != null) for (int i = 0; i < tb.Length; i++) h = (h ^ tb[i]) * 16777619u;
                s.TerrainHash = h;
                s.Terrain = tb;
            }
            catch (Exception e) { BtcLog.Warn("WorldSig", "terrain: " + e.Message); }
            Saved[tag] = s;
            try { Write(tag, s); } catch (Exception e) { BtcLog.Warn("WorldSig", "write: " + e.Message); }
            int act = 0, off = 0;
            foreach (KeyValuePair<string, int> kv in s.Kinds) { if (kv.Key.EndsWith("#off")) off += kv.Value; else act += kv.Value; }
            BtcLog.Info("WorldSig", tag + " t=" + BtcReplay.PlayClock.ToString("0.0") + " objs " + act + "+" + off + "off blocks " + s.Blocks.Count + " trees " + s.Trees.Count
                + " terrain " + s.TerrainLen + "B #" + s.TerrainHash.ToString("x8") + " units " + s.Renders.Count + " wardvis " + s.Wards.Count + " crystals " + s.Crystals.Count);
            BtcLog.Info("WorldSig", tag + " " + BtcDevDeblocProbe.Summary());
            Sig r;
            if (refTag == null) return;
            if (!Saved.TryGetValue(refTag, out r))
            {
                try { r = Load(refTag); } catch (Exception e) { BtcLog.Warn("WorldSig", "load " + refTag + ": " + e.Message); }
                if (r == null) { BtcLog.Warn("WorldSig", "no ref " + refTag); return; }
            }
            StringBuilder sb = new StringBuilder();
            HashSet<string> keys = new HashSet<string>(s.Kinds.Keys);
            keys.UnionWith(r.Kinds.Keys);
            foreach (string k in keys)
            {
                int a, b;
                s.Kinds.TryGetValue(k, out a);
                r.Kinds.TryGetValue(k, out b);
                if (a != b) sb.Append(k).Append(' ').Append(b).Append("->").Append(a).Append(IdDiff(s, r, k)).Append("; ");
            }
            BtcLog.Info("WorldSig", tag + " vs " + refTag + " kinds: " + (sb.Length > 0 ? Cut(sb) : "same"));
            sb.Length = 0;
            int pd = 0;
            foreach (KeyValuePair<uint, string> kv in s.Parent)
            {
                string rv;
                if (r.Parent.TryGetValue(kv.Key, out rv) && rv != kv.Value)
                {
                    pd++;
                    if (sb.Length < 600)
                    {
                        GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(kv.Key));
                        sb.Append(go != null ? Kind(go.name) : "?").Append(' ').Append(rv).Append("->").Append(kv.Value).Append("; ");
                    }
                }
            }
            BtcLog.Info("WorldSig", tag + " vs " + refTag + " parent diffs " + pd + ": " + Cut(sb));
            BtcLog.Info("WorldSig", tag + " vs " + refTag + " blocks " + SetDiff(s.Blocks, r.Blocks) + " | trees " + SetDiff(s.Trees, r.Trees)
                + " | unit renderers " + SetDiff(s.Renders, r.Renders) + " | terrain " + (s.TerrainHash == r.TerrainHash ? "same" : r.TerrainLen + "->" + s.TerrainLen + "B"));
            BtcLog.Info("WorldSig", tag + " vs " + refTag + " wards " + SetDiff(s.Wards, r.Wards) + " | crystals " + SetDiff(s.Crystals, r.Crystals));
            if (s.TerrainHash != r.TerrainHash) BtcLog.Info("WorldSig", tag + " vs " + refTag + " terrain diff: " + BtcReplayAnchor.DiffTerrain(s.Terrain, r.Terrain));
        }

        // BTClogs/ws/<tag>.txt: a reference taken in another game run (e.g. playback without preprocess) can be diffed later
        private static string FileOf(string tag) { return System.IO.Path.Combine(System.IO.Path.Combine(BtcDevPaths.LogDir, "ws"), tag + ".txt"); }

        private static void Write(string tag, Sig s)
        {
            StringBuilder sb = new StringBuilder(1 << 16);
            foreach (KeyValuePair<string, int> kv in s.Kinds) sb.Append("K\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            foreach (KeyValuePair<string, HashSet<uint>> kv in s.Ids) foreach (uint id in kv.Value) sb.Append("I\t").Append(kv.Key).Append('\t').Append(id).Append('\n');
            foreach (KeyValuePair<uint, string> kv in s.Parent) sb.Append("P\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            Lines(sb, "B", s.Blocks); Lines(sb, "T", s.Trees); Lines(sb, "R", s.Renders); Lines(sb, "W", s.Wards); Lines(sb, "C", s.Crystals);
            sb.Append("H\t").Append(s.TerrainLen).Append('\t').Append(s.TerrainHash).Append('\n');
            if (s.Terrain != null) sb.Append("X\t").Append(Convert.ToBase64String(s.Terrain)).Append('\n');
            string path = FileOf(tag);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, sb.ToString());
        }

        private static void Lines(StringBuilder sb, string k, HashSet<string> set)
        {
            foreach (string x in set) sb.Append(k).Append('\t').Append(x).Append('\n');
        }

        private static Sig Load(string tag)
        {
            string path = FileOf(tag);
            if (!System.IO.File.Exists(path)) return null;
            Sig s = new Sig();
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                string[] a = line.Split('\t');
                if (a.Length < 2) continue;
                switch (a[0])
                {
                    case "K": s.Kinds[a[1]] = int.Parse(a[2]); break;
                    case "I":
                        {
                            HashSet<uint> hs; if (!s.Ids.TryGetValue(a[1], out hs)) { hs = new HashSet<uint>(); s.Ids[a[1]] = hs; }
                            hs.Add(uint.Parse(a[2]));
                            break;
                        }
                    case "P": s.Parent[uint.Parse(a[1])] = a[2]; break;
                    case "B": s.Blocks.Add(a[1]); break;
                    case "T": s.Trees.Add(a[1]); break;
                    case "R": s.Renders.Add(a[1]); break;
                    case "W": s.Wards.Add(a[1]); break;
                    case "C": s.Crystals.Add(a[1]); break;
                    case "H": s.TerrainLen = int.Parse(a[1]); s.TerrainHash = uint.Parse(a[2]); break;
                    case "X": s.Terrain = Convert.FromBase64String(a[1]); break;
                }
            }
            Saved[tag] = s;
            return s;
        }

        /// <summary>What the eye sees of wards and crystals: every visible renderer/projector under a ward/capture/crystal root
        /// (networked or not, so pooled phantoms count), owner of each affiliated ward/crystal, crystal colour/creep state.</summary>
        private static void Visuals(Sig s)
        {
            foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                if (r.enabled) AddVis(s, r.transform);
            foreach (Projector pj in UnityEngine.Object.FindObjectsOfType<Projector>())
                if (pj.enabled) AddVis(s, pj.transform);
            foreach (Brix.Game.Components.AffiliationComponent af in UnityEngine.Object.FindObjectsOfType<Brix.Game.Components.AffiliationComponent>())
            {
                string n = af.gameObject.name;
                if (!IsWardOrCrystal(n)) continue;
                Brix.Game.Faction f = null;
                try { f = af.faction; } catch (Exception) { }
                s.Crystals.Add("own " + Kind(n) + "@" + P(af.transform.position) + "=" + (f != null ? f.lua_FactionName : "-"));
            }
            foreach (CrystalVisuals cv in UnityEngine.Object.FindObjectsOfType<CrystalVisuals>())
            {
                StringBuilder c = new StringBuilder();
                c.Append("cv@").Append(P(cv.transform.position)).Append(" c").Append((int)cv.CrystalColor).Append(" k").Append((int)cv.CreepColor)
                    .Append(" p").Append(Mathf.RoundToInt(cv.CreepProgression * 10f)).Append(" cp").Append(Mathf.RoundToInt(cv.CaptureProgress * 10f))
                    .Append(" g").Append(Mathf.RoundToInt(cv.Growth * 10f)).Append(cv.IsInTransition ? " tr" : "").Append(cv.IsActive ? "" : " off");
                Animator an = cv.GetComponent<Animator>();
                if (an != null && an.isActiveAndEnabled)
                    foreach (AnimatorControllerParameter ap in an.parameters)
                    {
                        if (ap.type == AnimatorControllerParameterType.Float) c.Append(' ').Append(ap.name.Replace(" ", "")).Append('=').Append(Mathf.RoundToInt(an.GetFloat(ap.nameHash) * 10f));
                        else if (ap.type == AnimatorControllerParameterType.Int) c.Append(' ').Append(ap.name.Replace(" ", "")).Append('=').Append(an.GetInteger(ap.nameHash));
                        else if (ap.type == AnimatorControllerParameterType.Bool) c.Append(' ').Append(ap.name.Replace(" ", "")).Append('=').Append(an.GetBool(ap.nameHash) ? 1 : 0);
                    }
                s.Crystals.Add(c.ToString());
            }
            foreach (Brix.Game.Components.CraftingStation cs in UnityEngine.Object.FindObjectsOfType<Brix.Game.Components.CraftingStation>())
            {
                string first = "-", pick = "-", work = "?", rec = "?";
                try { Brix.Game.Components.RecipeInfo ri = cs.FirstRecipe; first = ri != null && ri.Creates != null ? ri.Creates.ToString() : "-"; } catch (Exception) { }
                try { GameObject g; pick = cs.ProductionToPickup(out g) ? (g != null ? Kind(g.name) : "y") : "-"; } catch (Exception) { }
                try
                {
                    object st = typeof(Brix.Game.Components.CraftingStation).GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cs);
                    Brix.Game.Components.CraftingState cst = st as Brix.Game.Components.CraftingState;
                    if (cst != null) { work = cst.Working ? "1" : "0"; rec = typeof(Brix.Game.Components.CraftingState).GetField("recipes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cst).ToString(); }
                }
                catch (Exception) { }
                Brix.Game.Semantique.Recepteur rp = cs.GetComponent<Brix.Game.Semantique.Recepteur>();
                string inv = "";
                if (rp != null) foreach (Transform k in rp.GetComponentsInChildren<Transform>(false)) { NetworkIdentity ki = k.GetComponent<NetworkIdentity>(); if (ki != null && k != rp.transform) inv += Kind(k.name) + ","; }
                s.Crystals.Add("station " + Kind(cs.gameObject.name) + "@" + P(cs.transform.position) + " first " + first + " pickup " + pick + " work " + work + " rec " + rec + " inv " + inv);
            }
            foreach (CaptureClock cc in UnityEngine.Object.FindObjectsOfType<CaptureClock>())
            {
                Brix.Game.Faction cap = null, def = null;
                try { cap = cc.CurrentCapturers; def = cc.CaptureDefault; } catch (Exception) { }
                s.Crystals.Add("clock " + Kind(cc.gameObject.name) + "@" + P(cc.transform.position) + " r" + Mathf.RoundToInt(cc.CaptureRatio * 20f)
                    + " by " + (cap != null ? cap.lua_FactionName : "-") + " def " + (def != null ? def.lua_FactionName : "-"));
            }
            foreach (IsCapturable ic in UnityEngine.Object.FindObjectsOfType<IsCapturable>())
            {
                Brix.Game.Faction cap = null;
                try { cap = ic.CurrentCapturers; } catch (Exception) { }
                s.Crystals.Add("capt " + Kind(ic.gameObject.name) + "@" + P(ic.transform.position) + " r" + Mathf.RoundToInt(ic.CaptureRatio * 20f)
                    + " by " + (cap != null ? cap.lua_FactionName : "-") + (ic.DoneCapturing ? " done" : ""));
            }
        }

        private static void AddVis(Sig s, Transform t)
        {
            Transform root = t;
            while (root.parent != null && !IsWardOrCrystal(root.name)) root = root.parent;
            if (!IsWardOrCrystal(root.name)) return;
            LineRenderer lr = t.GetComponent<LineRenderer>();
            if (lr != null)
            {
                // a link line is drawn in world space: its end points are what is seen
                int n = lr.positionCount;
                s.Wards.Add(Kind(root.name) + " line " + n + (n > 0 ? " " + P(lr.GetPosition(0)) + ".." + P(lr.GetPosition(n - 1)) : "") + (lr.useWorldSpace ? "" : " local@" + P(t.position)));
                return;
            }
            NetworkIdentity rni = root.GetComponent<NetworkIdentity>();
            if (rni == null && _dumped.Add(root.GetInstanceID()) && _dumped.Count < 6)
            {
                string cs = "";
                foreach (Component c in root.GetComponents<Component>()) cs += (c != null ? c.GetType().Name : "null") + ",";
                string ch = "";
                foreach (Transform k in root) ch += k.name + (k.gameObject.activeSelf ? "" : "#off") + ",";
                string up = "";
                for (Transform u = root.parent; u != null; u = u.parent) up += u.name + "<";
                BtcLog.Info("WorldSig", "nonet " + root.name + " active " + root.gameObject.activeInHierarchy + " comps " + cs + " children " + ch + " up " + up);
            }
            string net = rni == null ? " nonet" : (ClientScene.FindLocalObject(rni.netId) == root.gameObject ? "" : " unreg" + rni.netId.Value);
            s.Wards.Add(Kind(root.name) + "@" + P(root.position) + ":" + Kind(t.name) + net + (root.parent != null ? " in:" + root.parent.name : ""));
        }

        private static bool IsWardOrCrystal(string n)
        {
            return n.IndexOf("Ward", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Capture", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static readonly HashSet<int> _dumped = new HashSet<int>();

        private static string P(Vector3 v)
        {
            return Mathf.RoundToInt(v.x) + "," + Mathf.RoundToInt(v.y) + "," + Mathf.RoundToInt(v.z);
        }

        private static string IdDiff(Sig now, Sig refSig, string k)
        {
            HashSet<uint> a, b;
            now.Ids.TryGetValue(k, out a);
            refSig.Ids.TryGetValue(k, out b);
            string o = "";
            if (a != null) foreach (uint x in a) if (b == null || !b.Contains(x)) o += " +" + x;
            if (b != null) foreach (uint x in b) if (a == null || !a.Contains(x)) o += " -" + x;
            return o.Length > 0 ? " [" + (o.Length > 120 ? o.Substring(0, 120) : o) + " ]" : "";
        }

        private static string SetDiff(HashSet<string> now, HashSet<string> refSet)
        {
            int extra = 0, miss = 0;
            string ex = "", mi = "";
            foreach (string x in now) if (!refSet.Contains(x)) { extra++; if (ex.Length < 400) ex += x + " "; }
            foreach (string x in refSet) if (!now.Contains(x)) { miss++; if (mi.Length < 400) mi += x + " "; }
            return (extra == 0 && miss == 0) ? "same" : ("+" + extra + " [" + ex + "] -" + miss + " [" + mi + "]");
        }

        private static string Kind(string n)
        {
            int i = n.IndexOf(" (");
            if (i > 0) n = n.Substring(0, i);
            return n.Replace("(Clone)", "");
        }

        private static void Inc(Dictionary<string, int> d, string k)
        {
            int n; d.TryGetValue(k, out n); d[k] = n + 1;
        }

        private static string Cut(StringBuilder sb)
        {
            return sb.Length > 900 ? sb.ToString(0, 900) + "..." : sb.ToString();
        }
    }
}
#endif
