#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Assets;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using Brix.Game.Semantique;
using Brix.Legacy;
using Smooth.Algebraics;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV: "ecodump [x0 z0 x1 z1]" writes BTClogs/eco-map.bin (format: docs/eco/DESIGN.md "File formats").
    /// One shot, synchronous (seconds on big maps). Crop box in voxel coords, default = occupied bounds of the loaded map.
    /// Y range per dump = lowest column bottom .. highest column top + YMargin (placed blocks above the terrain).
    /// </summary>
    internal static class BtcDevEcoDump
    {
        private const int YMargin = 24;
        private const long MaxCells = 300000000L;
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        internal static string K(Factory.AssetKey k)
        {
            try { return k.IsNullOrInvalid() ? "-" : k.Factory + "." + k.Name; } catch { return "-"; }
        }

        internal static string GK(GameComponent gc) { try { return K(gc.AssetKey); } catch { return "-"; } }

        internal static string Desc(Description d)
        {
            if (d == null) return "";
            try
            {
                DicoAdjectif dico = d.DicoAdjectif;
                if (dico == null) return "";
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<Type, Adjectif> kv in dico)
                {
                    Adjectif a = kv.Value;
                    if (a == null) continue;
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(a.GetKey().codeName);
                    Quantite q = a as Quantite;
                    if (q != null) sb.Append('*').Append(q.valeur);
                }
                return sb.ToString();
            }
            catch { return "?"; }
        }

        private static string F3(float f) { return f.ToString("0.###", Inv); }

        internal static void Run(string[] p)
        {
            float t0 = Time.realtimeSinceStartup;
            try { Do(p); }
            catch (Exception e) { BtcLog.Error("EcoDump", "failed: " + e); }
            BtcLog.Info("EcoDump", "done in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
        }

        private static void Do(string[] p)
        {
            if (!VoxelEngine.Ready) { BtcLog.Warn("EcoDump", "voxel engine not ready (load a game first)"); return; }
            Bounds bb = Voxel.Engine.ComputeOccupiedBounds();
            int x0 = Mathf.FloorToInt(bb.min.x), x1 = Mathf.CeilToInt(bb.max.x) - 1;
            int z0 = Mathf.FloorToInt(bb.min.z), z1 = Mathf.CeilToInt(bb.max.z) - 1;
            if (p != null && p.Length >= 5)
            {
                x0 = (int)F(p, 1); z0 = (int)F(p, 2); x1 = (int)F(p, 3); z1 = (int)F(p, 4);
                if (x1 < x0) { int t = x0; x0 = x1; x1 = t; }
                if (z1 < z0) { int t = z0; z0 = z1; z1 = t; }
            }
            int sx = x1 - x0 + 1, sz = z1 - z0 + 1;
            if (sx <= 0 || sz <= 0 || sx > 65535 || sz > 65535) { BtcLog.Warn("EcoDump", "bad box " + sx + "x" + sz); return; }
            int ymin = int.MaxValue, ymax = int.MinValue;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    int b, t;
                    VoxelEngine.GetColumnData(x, z, out b, out t);
                    if (t < b) continue;
                    if (b < ymin) ymin = b;
                    if (t > ymax) ymax = t;
                }
            if (ymin > ymax) { BtcLog.Warn("EcoDump", "no columns in box"); return; }
            ymax += YMargin;
            int sy = ymax - ymin + 1;
            if (sy > 65535 || (long)sx * sy * sz > MaxCells) { BtcLog.Warn("EcoDump", "box too large " + sx + "x" + sy + "x" + sz + "; use a crop box"); return; }
            BtcLog.Info("EcoDump", "box x " + x0 + ".." + x1 + " z " + z0 + ".." + z1 + " y " + ymin + ".." + ymax);

            string m0 = "";
            long n = (long)sx * sy * sz;
            byte[] terr = new byte[n];
            ushort[] blk = new ushort[n];
            List<string> blkKeys = new List<string>();
            List<string> blkRes = new List<string>();
            Dictionary<string, int> blkIdx = new Dictionary<string, int>();
            int terrCount = 0, blkCount = 0, oreCount = 0;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    int b, t;
                    VoxelEngine.GetColumnData(x, z, out b, out t);
                    long baseIdx = ((long)(x - x0) * sz + (z - z0)) * sy;
                    int top = t + YMargin;
                    for (int y = Math.Max(b, ymin); y <= top && y <= ymax; y++)
                    {
                        XYZ pos = new XYZ(x, y, z);
                        try
                        {
                            if (!Voxel.IsNonEmpty(pos)) continue;
                            using (IVoxel v = Voxel.GetVoxel(pos))
                            {
                                if (v == null || v.IsNull) continue;
                                BlockInfo bi = v.BlockInfo;
                                long idx = baseIdx + (y - ymin);
                                if (v.IsTerrain)
                                {
                                    int id = bi != null ? bi.ID : 0;
                                    terr[idx] = (byte)(id > 255 ? 255 : (id < 0 ? 0 : id));
                                    terrCount++;
                                    if (id == 54 || id == 44 || id == 38 || id == 55 || id == 57 || id == 59) oreCount++;
                                }
                                else if (bi != null)
                                {
                                    string key = K(bi.Key);
                                    int bi1;
                                    if (!blkIdx.TryGetValue(key, out bi1))
                                    {
                                        blkKeys.Add(key); blkRes.Add(K(bi.ResourceKey));
                                        bi1 = blkKeys.Count; blkIdx[key] = bi1;
                                    }
                                    blk[idx] = (ushort)bi1;
                                    blkCount++;
                                }
                            }
                        }
                        catch { }
                    }
                }
            m0 = "param|oreVoxels|" + oreCount + "\n";
            BtcLog.Info("EcoDump", "ore voxels " + oreCount + "; voxels: terrain " + terrCount + " blocks " + blkCount + " blockKinds " + blkKeys.Count);

            StringBuilder m = new StringBuilder(1 << 16);
            m.Append(m0);
            try { m.Append("gp|").Append(BtcGameplay.Encode(BtcGameplay.Active)).Append('\n'); } catch (Exception e) { m.Append("gp|?\n"); BtcLog.Warn("EcoDump", "gp: " + e.Message); }
            for (int i = 0; i < blkKeys.Count; i++) m.Append("blk|").Append(i + 1).Append('|').Append(blkKeys[i]).Append('|').Append(blkRes[i]).Append('\n');
            Trees(m, x0, x1, z0, z1);
            NatureNodes(m, x0, x1, z0, z1);
            Entities(m, x0, x1, z0, z1);
            Blueprints(m);
            Recipes(m);
            m.Append("param|dumpBox|").Append(x0).Append(',').Append(z0).Append(',').Append(x1).Append(',').Append(z1).Append('\n');
            m.Append("param|yMargin|").Append(YMargin).Append('\n');
            try { Asset_Map map = GameParam.Map; if (map != null) m.Append("param|map|").Append(map.displayName).Append('\n'); } catch { }

            string path = System.IO.Path.Combine(BtcDevPaths.LogDir, "eco-map.bin");
            Directory.CreateDirectory(BtcDevPaths.LogDir);
            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write(new byte[] { (byte)'B', (byte)'T', (byte)'C', (byte)'E' });
                w.Write((ushort)1);
                w.Write(x0); w.Write(ymin); w.Write(z0);
                w.Write((ushort)sx); w.Write((ushort)sy); w.Write((ushort)sz);
                w.Write(terr);
                byte[] bb2 = new byte[n * 2];
                Buffer.BlockCopy(blk, 0, bb2, 0, bb2.Length); // little-endian host
                w.Write(bb2);
                byte[] meta = Encoding.UTF8.GetBytes(m.ToString());
                w.Write(meta.Length);
                w.Write(meta);
            }
            BtcLog.Info("EcoDump", "wrote " + path + " (" + new FileInfo(path).Length + " bytes)");
        }

        private static float F(string[] p, int i)
        {
            float f; return i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, Inv, out f) ? f : 0f;
        }

        private static bool In(float x, float z, int x0, int x1, int z0, int z1)
        {
            return x >= x0 - 1 && x <= x1 + 1 && z >= z0 - 1 && z <= z1 + 1;
        }

        private static void Trees(StringBuilder m, int x0, int x1, int z0, int z1)
        {
            try
            {
                TreeRegistry tr = TreeRegistry.Instance;
                if (tr == null) return;
                int c = 0;
                foreach (XYZ t in tr.ListAllTreePosition())
                {
                    if (!In(t.x, t.z, x0, x1, z0, z1)) continue;
                    m.Append("tree|").Append(t.x).Append('|').Append(t.y).Append('|').Append(t.z).Append('|').Append(tr.GetTree(t)).Append('\n');
                    c++;
                }
                BtcLog.Info("EcoDump", "trees " + c);
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "trees: " + e.Message); }
        }

        private static void UnitInv(GameObject go, Labor l, StringBuilder kv)
        {
            try { kv.Append("inv=").Append(Desc(l.recepteur != null ? l.recepteur.ContentDescription : null).Replace(';', ':')).Append(';'); } catch { }
            try { kv.Append("invcap=").Append(Desc(l.recepteur != null ? l.recepteur.BaseCapacity : null).Replace(';', ':')).Append(';'); } catch { }
            try
            {
                Toolbag tb = l.toolbag != null ? l.toolbag : go.GetComponent<Toolbag>();
                if (tb != null)
                {
                    List<GameObject> tools = new List<GameObject>();
                    tb.Tools(ref tools, false);
                    StringBuilder ts = new StringBuilder();
                    for (int i = 0; i < tools.Count; i++)
                    {
                        if (tools[i] == null) continue;
                        GameComponent tg = tools[i].GetComponent<GameComponent>();
                        if (ts.Length > 0) ts.Append(',');
                        ts.Append(tg != null ? GK(tg) : tools[i].name);
                    }
                    kv.Append("tool=").Append(ts).Append(';');
                }
            }
            catch (Exception e) { kv.Append("tool=?;"); BtcLog.Warn("EcoDump", "tool: " + e.Message); }
        }

        private static void NatureNodes(StringBuilder m, int x0, int x1, int z0, int z1)
        {
            int cp = 0, cb = 0, ci = 0, cy = 0;
            try
            {
                PlantSerializedData[] pl = PlantRegistry.Instance != null ? PlantRegistry.Instance.SerializedData : null;
                if (pl != null)
                    for (int i = 0; i < pl.Length; i++)
                    {
                        if (!In(pl[i].x, pl[i].z, x0, x1, z0, z1)) continue;
                        m.Append("nature|plant|").Append(pl[i].x).Append('|').Append(pl[i].y).Append('|').Append(pl[i].z).Append('|').Append(F3(pl[i].s)).Append("|2|t=").Append(pl[i].t).Append('\n');
                        cp++;
                    }
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "plants: " + e.Message); }
            try
            {
                List<XYZ> bl = new List<XYZ>();
                BoulderRegistry.AddAllBoulderPositionsToList(ref bl);
                for (int i = 0; i < bl.Count; i++)
                {
                    XYZ q = bl[i];
                    if (!In(q.x, q.z, x0, x1, z0, z1)) continue;
                    BoulderData b = BoulderRegistry.GetBoulder(q);
                    string ex = "";
                    float g = 1f; int cur = 0, max = 0;
                    if (b != null) { g = b.Growth; try { cur = b.CurrentRessources; max = b.MaxRessources; } catch { } ex = "size=" + b.BoulderSize + ";var=" + b.Variation + ";max=" + max; }
                    m.Append("nature|boulder|").Append(q.x).Append('|').Append(q.y).Append('|').Append(q.z).Append('|').Append(F3(g)).Append('|').Append(cur).Append('|').Append(ex).Append('\n');
                    cb++;
                }
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "boulders: " + e.Message); }
            try
            {
                MineralSerializedData[] ir = IronRegistry.GetSerializedMinerals();
                if (ir != null)
                    for (int i = 0; i < ir.Length; i++)
                    {
                        XYZ q = ir[i].position;
                        if (!In(q.x, q.z, x0, x1, z0, z1)) continue;
                        int cur = 0, max = 0; try { MineralData md = IronRegistry.GetMineral(q); if (md != null) { cur = md.CurrentRessources; max = md.MaxRessources; } } catch { }
                        m.Append("nature|iron|").Append(q.x).Append('|').Append(q.y).Append('|').Append(q.z).Append('|').Append(F3(ir[i].growth)).Append('|').Append(cur).Append("|var=").Append(ir[i].variation).Append(";max=").Append(max).Append('\n');
                        ci++;
                    }
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "iron: " + e.Message); }
            try
            {
                MineralSerializedData[] py = PyriteRegistry.GetSerializedMinerals();
                if (py != null)
                    for (int i = 0; i < py.Length; i++)
                    {
                        XYZ q = py[i].position;
                        if (!In(q.x, q.z, x0, x1, z0, z1)) continue;
                        int cur = 0, max = 0; try { MineralData md = PyriteRegistry.GetMineral(q); if (md != null) { cur = md.CurrentRessources; max = md.MaxRessources; } } catch { }
                        m.Append("nature|pyrite|").Append(q.x).Append('|').Append(q.y).Append('|').Append(q.z).Append('|').Append(F3(py[i].growth)).Append('|').Append(cur).Append("|var=").Append(py[i].variation).Append(";max=").Append(max).Append('\n');
                        cy++;
                    }
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "pyrite: " + e.Message); }
            BtcLog.Info("EcoDump", "nature plant " + cp + " boulder " + cb + " iron " + ci + " pyrite " + cy);
        }

        private static string Kind(GameObject go, out string key, StringBuilder kv)
        {
            key = "-";
            GameComponent gc = go.GetComponent<GameComponent>();
            if (gc != null) key = GK(gc);
            if (go.GetComponent<Labor>() != null)
            {
                Labor l = go.GetComponent<Labor>();
                try { kv.Append("prof=").Append(l.Profession != null ? l.Profession.GetType().Name : "-").Append(';'); } catch { }
                try { kv.Append("auto=").Append(l.Autonomy.Id).Append(';'); } catch { }
                UnitInv(go, l, kv);
                return "unit";
            }
            if (go.GetComponent<Blueprint>() != null) { Blueprint b = go.GetComponent<Blueprint>(); kv.Append("anti=").Append(b.IsAnti ? 1 : 0).Append(";bp=").Append(K(b.BlueprintResult)).Append(';'); return "blueprint"; }
            CraftingStation cs = go.GetComponent<CraftingStation>();
            if (cs != null) { kv.Append("type=").Append(cs.GetType().Name).Append(';'); return "station"; }
            ToolRack tr = go.GetComponent<ToolRack>();
            if (tr != null)
            {
                kv.Append("hal=").Append(tr.IsForHalberdier ? 1 : 0).Append(";knight=").Append(tr.IsForKnight ? 1 : 0).Append(";archer=").Append(tr.IsForArcher ? 1 : 0)
                  .Append(";arb=").Append(tr.IsForArbalist ? 1 : 0).Append(";alch=").Append(tr.IsForAlchemist ? 1 : 0).Append(";art=").Append(tr.IsForArtificer ? 1 : 0)
                  .Append(";cap=").Append(ToolRack.TOOLRACK_CAPACITY).Append(';');
                return "rack";
            }
            if (go.GetComponent<PassThroughStockpile>() != null || go.GetComponent<ResourceConverter>() != null) return "stockpile";
            Apparence ap = go.GetComponent<Apparence>();
            if (ap != null && ap.Principale != null)
            {
                if (ap.Principale is CrystalObjective) return "crystal";
                if (go.GetComponent<Recepteur>() == null) { try { kv.Append("res=").Append(ap.Principale.GetKey().codeName).Append(';'); } catch { } return "item"; }
            }
            try { MonoBehaviour[] mb = go.GetComponents<MonoBehaviour>(); kv.Append("comps="); for (int i = 0; i < mb.Length; i++) if (mb[i] != null) kv.Append(mb[i].GetType().Name).Append(i + 1 < mb.Length ? "," : ""); kv.Append(';'); } catch { }
            return "building";
        }

        private static void Entities(StringBuilder m, int x0, int x1, int z0, int z1)
        {
            HashSet<string> caps = new HashSet<string>();
            int c = 0;
            try
            {
                NetworkIdentity[] all = UnityEngine.Object.FindObjectsOfType<NetworkIdentity>();
                for (int i = 0; i < all.Length; i++)
                {
                    NetworkIdentity ni = all[i];
                    if (ni == null) continue;
                    GameObject go = ni.gameObject;
                    Vector3 pos = go.transform.position;
                    if (!In(pos.x, pos.z, x0, x1, z0, z1)) continue;
                    StringBuilder kv = new StringBuilder();
                    string key;
                    string kind = Kind(go, out key, kv);
                    Recepteur rc = go.GetComponent<Recepteur>();
                    if (rc != null)
                    {
                        try
                        {
                            kv.Append("content=").Append(Desc(rc.ContentDescription).Replace(';', ':')).Append(';');
                            kv.Append("cap=").Append(Desc(rc.BaseCapacity).Replace(';', ':')).Append(';');
                            kv.Append("exclAll=").Append(rc.ExcludeAll ? 1 : 0).Append(";excl=").Append(Desc(new Description(rc.Exclusions)).Replace(';', ':')).Append(';');
                            if (caps.Add(key)) m.Append("cap|").Append(key).Append('|').Append(Desc(rc.BaseCapacity)).Append('\n');
                        }
                        catch { }
                    }
                    CraftingStation cs = go.GetComponent<CraftingStation>();
                    if (cs != null)
                    {
                        try { kv.Append("queue=").Append(cs.QueueCount).Append(";loop=").Append(cs.Looping ? 1 : 0).Append(';'); } catch { }
                    }
                    if (kind == "building" || kind == "other")
                    {
                        string nat = NatureOf(go);
                        if (nat != null) { m.Append("nature|").Append(nat).Append('|').Append(F3(pos.x)).Append('|').Append(F3(pos.y)).Append('|').Append(F3(pos.z)).Append('\n'); c++; continue; }
                    }
                    m.Append("ent|").Append(ni.netId.Value).Append('|').Append(kind).Append('|').Append(key).Append('|')
                     .Append(F3(pos.x)).Append('|').Append(F3(pos.y)).Append('|').Append(F3(pos.z)).Append('|').Append(F3(go.transform.eulerAngles.y)).Append('|').Append(kv).Append('\n');
                    c++;
                }
                Project[] ps = UnityEngine.Object.FindObjectsOfType<Project>();
                for (int i = 0; i < ps.Length; i++)
                {
                    Project pr = ps[i];
                    if (pr == null) continue;
                    NetworkIdentity ni = pr.GetComponentInParent<NetworkIdentity>();
                    Vector3 pos = pr.transform.position;
                    m.Append("ent|").Append(ni != null ? ni.netId.Value : 0u).Append("|project|").Append(pr.GetType().Name).Append('|')
                     .Append(F3(pos.x)).Append('|').Append(F3(pos.y)).Append('|').Append(F3(pos.z)).Append("|0|goals=").Append(pr.GoalCounts()).Append(";crew=").Append(pr.CrewCount()).Append(";\n");
                    c++;
                }
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "entities: " + e); }
            BtcLog.Info("EcoDump", "entities " + c);
        }

        private static string NatureOf(GameObject go)
        {
            MonoBehaviour[] cs = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i] == null) continue;
                string n = cs[i].GetType().Name;
                if (n.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0) return "plant";
                if (n.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0) return "boulder";
                if (n.IndexOf("Pyrite", StringComparison.OrdinalIgnoreCase) >= 0) return "pyrite";
                if (n.IndexOf("Mineral", StringComparison.OrdinalIgnoreCase) >= 0) return "iron";
            }
            return null;
        }

        private static void Blueprints(StringBuilder m)
        {
            int c = 0;
            try
            {
                // 1) every block kind (BlockInfo): cost = ResourceKey per block (+ Ressource type), footprint by BlockInfo.Volume.
                FieldInfo fk = typeof(BlockInfoGenerator).GetField("_blockKeyToBlockInfos", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                System.Collections.IDictionary d = fk != null ? fk.GetValue(null) as System.Collections.IDictionary : null;
                HashSet<string> seenBp = new HashSet<string>();
                if (d != null)
                    foreach (System.Collections.DictionaryEntry de in d)
                    {
                        BlockInfo bi = de.Value as BlockInfo;
                        if (bi == null) continue;
                        string bpk = K(bi.BlueprintKey);
                        seenBp.Add(bpk);
                        string fp = "";
                        try
                        {
                            IVolume vol = bi.Volume(XYZ.zero, 0, 0);
                            StringBuilder fs = new StringBuilder();
                            foreach (KeyValuePair<XYZ, VPropertyMask> kv in (IEnumerable<KeyValuePair<XYZ, VPropertyMask>>)vol)
                            { if (fs.Length > 0) fs.Append(','); fs.Append(kv.Key.x).Append(',').Append(kv.Key.y).Append(',').Append(kv.Key.z); }
                            fp = fs.ToString();
                        }
                        catch { fp = "1x1x1?"; }
                        m.Append("bp|").Append(bpk).Append("|block:").Append(K(bi.Key)).Append('|').Append(K(bi.ResourceKey)).Append("*1|").Append(fp)
                         .Append("|id=").Append(bi.ID).Append(";partial=").Append(bi.IsPartialBloc ? 1 : 0).Append(";type=").Append(bi.Type).Append(";surface=").Append(bi.Surface).Append('\n');
                        c++;
                    }
                // 2) all Blueprints.* keys (cost/footprint of non-block buildings is not exposed without prefab spawn)
                foreach (FieldInfo f in typeof(Blueprints).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.FieldType != typeof(Factory.AssetKey)) continue;
                    string k = K((Factory.AssetKey)f.GetValue(null));
                    if (seenBp.Contains(k)) continue;
                    m.Append("bp|").Append(k).Append("|Blueprints||?|?|field=").Append(f.Name).Append('\n');
                    c++;
                }
            }
            catch (Exception e) { BtcLog.Warn("EcoDump", "blueprints: " + e.Message); }
            BtcLog.Info("EcoDump", "blueprints " + c);
        }

        private static void Recipes(StringBuilder m)
        {
            int c = 0, miss = 0;
            for (int i = 0; i < 400 && miss < 40; i++)
            {
                RecipeInfo r = null;
                try { r = CookBook.GetById(i); } catch { }
                if (r == null) { miss++; continue; }
                miss = 0;
                StringBuilder ing = new StringBuilder();
                try
                {
                    FieldInfo fi = typeof(RecipeInfo).GetField("ingredients", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    ing.Append(Desc(fi != null ? fi.GetValue(r) as Description : null));
                }
                catch { }
                m.Append("recipe|").Append(r.id).Append('|').Append(K(r.Creates)).Append('|').Append(ing).Append('|').Append(r.work).Append('|').Append(r.heat)
                 .Append('|').Append(K(r.Creates)).Append('|').Append(r.QtyCreated).Append('\n');
                c++;
            }
            BtcLog.Info("EcoDump", "recipes " + c);
        }
    }
}
#endif
