#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Components.Volumes;
using Brix.Game.Factories;
using Brix.Transactions;
using Brix.Utils;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: "chopdump [on|off [tag]]" -> BTClogs/chop-dump.txt (or chop-tag.txt). Volume cells (+ decoded VPropertyMask) of the chopping block blueprint and the
    /// built block for every rotation/config, prefab placement info; with a tag also places the blueprint for every rotation and logs Validate().</summary>
    internal static class BtcDevChopDump
    {
        internal static string Decode(long m)
        {
            if (m == 0) return "-";
            StringBuilder s = new StringBuilder();
            foreach (VProperty v in Enum.GetValues(typeof(VProperty)))
            {
                if ((m & (1L << (int)v)) != 0) { if (s.Length > 0) s.Append('|'); s.Append(v); }
            }
            return s.ToString();
        }

        internal static void Dump(string[] p)
        {
            if (p != null && p.Length > 1) BtcChopBlock1x1.Force = p[1] == "on" ? 1 : p[1] == "off" ? 0 : -1;
            bool test = p != null && p.Length > 2;
            StringBuilder sb = new StringBuilder(32768);
            try
            {
                Factory.AssetKey[] keys = { Blueprints.ChoppingBlock, ObjetsDynamiques.ChoppingBlock };
                foreach (Factory.AssetKey key in keys)
                {
                    sb.Append("##### ").Append(key).Append('\n');
                    Prefab(sb, key);
                    int nrot = PreprocessedRotations.DiscreteRotationForIndexes.Length;
                    for (int cfg = 0; cfg < 3; cfg++)
                    {
                        for (int rot = 0; rot < nrot; rot++)
                        {
                            Dictionary<XYZ, VPropertyMask> d;
                            try { d = VolumePreprocessor.GetIndexedVSettings(key, rot, cfg); }
                            catch (Exception e) { sb.Append("cfg ").Append(cfg).Append(" rot ").Append(rot).Append(" EXC ").Append(e.GetType().Name).Append('\n'); break; }
                            if (d == null) { sb.Append("cfg ").Append(cfg).Append(" rot ").Append(rot).Append(" null\n"); break; }
                            sb.Append("cfg ").Append(cfg).Append(" rot ").Append(rot).Append(" euler=").Append(PreprocessedRotations.DiscreteRotationForIndexes[rot].eulerAngles).Append(" cells=").Append(d.Count).Append('\n');
                            if (test && rot > 5) continue;
                            List<KeyValuePair<XYZ, VPropertyMask>> l = new List<KeyValuePair<XYZ, VPropertyMask>>(d);
                            l.Sort(delegate(KeyValuePair<XYZ, VPropertyMask> a, KeyValuePair<XYZ, VPropertyMask> b) { int c = a.Key.y.CompareTo(b.Key.y); if (c == 0) c = a.Key.x.CompareTo(b.Key.x); if (c == 0) c = a.Key.z.CompareTo(b.Key.z); return c; });
                            foreach (KeyValuePair<XYZ, VPropertyMask> kv in l) sb.Append("  ").Append(kv.Key.x).Append(',').Append(kv.Key.y).Append(',').Append(kv.Key.z).Append(' ').Append(Decode((long)kv.Value)).Append('\n');
                            if (rot < 2)
                            {
                                List<XYZ>[] ix = VolumePreprocessor.GetVSettingsIndices(key, rot, cfg);
                                for (int i = 0; i < ix.Length; i++)
                                {
                                    if (ix[i] == null || ix[i].Count == 0) continue;
                                    sb.Append("  idx ").Append((VProperty)i).Append(':');
                                    foreach (XYZ c in ix[i]) sb.Append(' ').Append(c.x).Append(',').Append(c.y).Append(',').Append(c.z);
                                    sb.Append('\n');
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) { sb.Append("EXC ").Append(e).Append('\n'); }
            try { if (test) Validate(sb); } catch (Exception e) { sb.Append("EXC validate ").Append(e).Append('\n'); }
            File.WriteAllText(System.IO.Path.Combine(BtcDevPaths.LogDir, test ? "chop-" + p[2] + ".txt" : "chop-dump.txt"), sb.ToString());
            BtcLog.Info("Dev", "chopdump written");
        }

        /// <summary>Place the blueprint at free flat spots for every rotation and log the validator result.</summary>
        internal static void Validate(StringBuilder sb)
        {
            sb.Append("##### validation test enabled=").Append(BtcChopBlock1x1.Enabled).Append('\n');
            Faction f = User.LocalUser != null ? User.LocalUser.faction : null;
            Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
            if (f == null || occ.Length == 0) { sb.Append("no faction/unit\n"); return; }
            Vector3 basep = occ[0].transform.position;
            sb.Append("unit pos=").Append(basep).Append('\n');
            int[] dxs = { 4, 7 };
            int[] dys = { 0, 8 };
            foreach (int dy in dys)
            foreach (int dx in dxs)
            {
                Vector3 pos = new Vector3(Mathf.Round(basep.x) + dx, Mathf.Round(basep.y) + dy, Mathf.Round(basep.z));
                sb.Append("-- spot dx=").Append(dx).Append(" dy=").Append(dy).Append('\n');
                for (int rot = 0; rot < PreprocessedRotations.DiscreteRotationForIndexes.Length; rot++)
                {
                    GameObject go = null;
                    try
                    {
                        go = Transactor.SpawnUNet(Blueprints.ChoppingBlock, f, pos, PreprocessedRotations.DiscreteRotationForIndexes[rot], null, true);
                        if (go == null) { sb.Append("rot ").Append(rot).Append(" spawn null\n"); continue; }
                        BaseVoxelTransform vt = go.GetComponent<BaseVoxelTransform>();
                        ValidatorComponent vc = go.GetComponent<ValidatorComponent>();
                        Blueprint bp = go.GetComponent<Blueprint>();
                        IVolume vol = go.GetComponent(typeof(IVolume)) as IVolume;
                        VUtil.NewVID();
                        ValidationResult r = vc.Validate(null);
                        XYZ pv = vt.position;
                        sb.Append("rot ").Append(rot).Append(" vtRot=").Append(vt.RotationIndex).Append(" pivot=").Append(pv.x).Append(',').Append(pv.y).Append(',').Append(pv.z)
                            .Append(" result=").Append(r).Append(" state=").Append(bp.State)
                            .Append(" fantome=").Append(vol.GetCases(VProperty.fantome).Count).Append(" occupy=").Append(vol.GetCases(VProperty.occupy).Count)
                            .Append(" support=").Append(vol.GetCases(VProperty.mainSupportReq).Count).Append('\n');
                    }
                    catch (Exception e) { sb.Append("rot ").Append(rot).Append(" EXC ").Append(e.GetType().Name).Append(' ').Append(e.Message).Append('\n'); }
                    finally { if (go != null) { try { UnityEngine.Networking.NetworkServer.Destroy(go); } catch { UnityEngine.Object.Destroy(go); } } }
                }
            }
        }

        private static void Prefab(StringBuilder sb, Factory.AssetKey key)
        {
            GameObject go = Factory.Peek(key);
            if (go == null) { sb.Append("prefab null\n"); return; }
            sb.Append("prefab ").Append(go.name).Append(" pos=").Append(go.transform.position).Append(" scale=").Append(go.transform.localScale).Append('\n');
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                sb.Append("  comp ").Append(c.transform == go.transform ? "" : c.transform.name + ":").Append(c.GetType().Name).Append('\n');
            }
            BaseVoxelTransform bv = go.GetComponent<BaseVoxelTransform>();
            if (bv != null) sb.Append("  BaseVoxelTransform ").Append(bv.GetType().Name).Append(" InitialCenterOffset=").Append(bv.InitialCenterOffset).Append(" PlacementOffset=").Append(bv.PlacementOffset).Append(" RangeTargetOffset=").Append(bv.RangeTargetOffset).Append('\n');
            Blueprint bp = go.GetComponent<Blueprint>();
            if (bp != null) sb.Append("  Blueprint result=").Append(bp.BlueprintResult).Append('\n');
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) sb.Append("  collider ").Append(col.GetType().Name).Append(" bounds=").Append(col.bounds).Append(" trigger=").Append(col.isTrigger).Append('\n');
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) sb.Append("  renderer ").Append(r.name).Append(" bounds=").Append(r.bounds).Append('\n');
            ValidatorComponent vc = go.GetComponent<ValidatorComponent>();
            if (vc != null) sb.Append("  Validator type=").Append(vc.validatorType != null ? vc.validatorType.ToString() : "null").Append(" MaxMissingSupport=").Append(vc.MaxMissingSupport).Append(" CanPlaceInvalid=").Append(vc.CanPlaceInvalid).Append('\n');
            IVolume v = go.GetComponent(typeof(IVolume)) as IVolume;
            sb.Append("  IVolume ").Append(v != null ? v.GetType().Name : "none").Append('\n');
        }
    }
}
#endif
