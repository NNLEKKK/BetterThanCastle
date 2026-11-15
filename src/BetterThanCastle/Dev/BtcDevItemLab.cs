#if DEV
using System;
using System.Collections.Generic;
using System.Text;
using Brix.Assets;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Network;
using Brix.Legacy;
using Brix.Lifecycle.Pooling;
using Brix.Lua;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>DEV lab for "mov.items" (pass through loose items): a worker is enclosed by a ring of loose blocks, then leaves by a manual order
    /// (itemlab manual) or by a project he is hired into (itemlab auto). itemlab report logs distance from the ring centre. Run with mov.items=1 and =0 (control).</summary>
    internal static class BtcDevItemLab
    {
        private static Vector3 _c;
        private static readonly List<Labor> Ws = new List<Labor>();
        private static readonly List<GameObject> Ring = new List<GameObject>();

        private static Vector3 Base()
        {
            Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
            return occ.Length > 0 ? occ[0].transform.position : Vector3.zero;
        }

        internal static void Setup(string item, int radius)
        {
            try
            {
                Ws.Clear(); Ring.Clear();
                Faction f = User.LocalUser.faction;
                XYZ p = Architecte.PigerUneCase(Base() + new Vector3(0f, 0f, -6f), 3f, 30);
                _c = p.Equals(XYZ.zero) ? Base() + new Vector3(0f, 0.5f, -6f) : p.ToVector3() + Vector3.up * 0.5f;
                int dot = item.IndexOf('.');
                Factory.AssetKey ik = new Factory.AssetKey(item.Substring(0, dot), item.Substring(dot + 1));
                int made = 0, libre = 0;
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                        GameObject go;
                        ObjectPoolSingleton.Request(out go, ik, _c + new Vector3(dx, 0.3f, dz), Quaternion.Euler(0f, 90f * ((dx + dz) & 1), 0f));
                        if (go == null) continue;
                        UnityEngine.Networking.NetworkServer.Spawn(go, ik.AssetId);
                        go.GetOrAddComponent<AffiliationComponent>().faction = f;
                        Rigidbody rb = go.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
                        made++; Ring.Add(go);
                        if (go.GetComponent<BlocLibre>() != null) libre++;
                    }
                LuaStaticDriver sd;
                if (LuaDriverHQ.AllStatics.TryGetValue("fy_Bricktron", out sd) && sd != null)
                {
                    LuaDriver w = LuaFactory.InstantiateFromCLR(sd.AssetKey, f, _c + Vector3.up * 0.2f, Quaternion.identity);
                    Labor l = w != null ? w.GetComponent<Labor>() : null;
                    if (l != null) Ws.Add(l);
                }
                BtcLog.Info("ItemLab", "setup item=" + item + " ring=" + made + " blocLibre=" + libre + " workers=" + Ws.Count + " centre=" + _c);
            }
            catch (Exception e) { BtcLog.Error("ItemLab", "setup: " + e); }
        }

        internal static void Manual()
        {
            try
            {
                for (int i = 0; i < Ws.Count; i++)
                    if (Ws[i] != null) Order.Move.RunToward.Send(Ws[i], Location.Of(_c + new Vector3(9f, 0f, 0f)), Labor.DecisionSource.User);
                BtcLog.Info("ItemLab", "manual order sent to " + Ws.Count);
            }
            catch (Exception e) { BtcLog.Error("ItemLab", "manual: " + e); }
        }

        internal static void Auto()
        {
            try
            {
                int n = 0;
                foreach (LandscapeProject p in UnityEngine.Object.FindObjectsOfType<LandscapeProject>())
                    for (int i = 0; i < Ws.Count; i++)
                        if (Ws[i] != null && p.Hire(Ws[i])) n++;
                BtcLog.Info("ItemLab", "hired into projects: " + n);
            }
            catch (Exception e) { BtcLog.Error("ItemLab", "auto: " + e); }
        }

        internal static void Report(string tag)
        {
            try
            {
                StringBuilder sb = new StringBuilder("ItemLab " + tag + ":");
                for (int i = 0; i < Ws.Count; i++)
                {
                    Labor l = Ws[i];
                    if (l == null) { sb.Append(" w" + i + "=dead"); continue; }
                    Vector3 d = l.transform.position - _c; d.y = 0f;
                    sb.Append(" w" + i + " dist=" + d.magnitude.ToString("0.0") + " task=" + (l.CurrentTask != null ? l.CurrentTask.ToString() : "none"));
                }
                float rs = 0f; int rn = 0;
                for (int i = 0; i < Ring.Count; i++) if (Ring[i] != null) { Vector3 d = Ring[i].transform.position - _c; d.y = 0f; rs += d.magnitude; rn++; }
                sb.Append(" ringR=" + (rn > 0 ? rs / rn : 0f).ToString("0.0") + " items=" + rn + "/" + Ring.Count);
                BtcLog.Info("ItemLab", sb.ToString());
            }
            catch (Exception e) { BtcLog.Error("ItemLab", "report: " + e); }
        }
    }
}
#endif
