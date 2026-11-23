#if DEV
using System;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lifecycle.Pooling;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>DEV stress lab: loose items scattered around the base (stressitems), all visual cuts at once (vfx max), sun shadows on in the hidden game (vfx sunshadows).</summary>
    internal static class BtcDevStress
    {
        internal static void Max(bool on)
        {
            BtcConfig.UnitNightLights.Value = !on; BtcConfig.FireflyLights.Value = !on; BtcConfig.CrystalShadows.Value = !on; BtcConfig.CrystalLights.Value = !on;
            BtcConfig.PauseHiddenAnims.Value = true; BtcConfig.HideNameTags.Value = on; BtcConfig.StaticCrystals.Value = on; BtcConfig.SimpleFireflies.Value = on;
            BtcConfig.ShortShadows.Value = on; BtcConfig.NoAmbientOcclusion.Value = on; BtcConfig.NoBloom.Value = on; BtcConfig.NoSunShadows.Value = on;
        }

        internal static void SunShadows(bool on)
        {
            QualitySettings.shadows = on ? ShadowQuality.All : ShadowQuality.Disable;
            BtcLog.Info("Cmd", "shadows=" + QualitySettings.shadows + " dist=" + QualitySettings.shadowDistance + " cascades=" + QualitySettings.shadowCascades);
        }

        // n loose items of one kind (Factory.Name, e.g. Blocks.Wood_DoubleLog_AA) on free cells within r of the first unit, physics on
        internal static void Items(string key, int n, float r)
        {
            try
            {
                Occupation[] occ = UnityEngine.Object.FindObjectsOfType<Occupation>();
                Faction f = User.LocalUser != null ? User.LocalUser.faction : null;
                if (occ.Length == 0 || f == null) { BtcLog.Warn("Stress", "no unit/faction"); return; }
                Vector3 b = occ[0].transform.position;
                int dot = key.IndexOf('.');
                Factory.AssetKey ik = new Factory.AssetKey(key.Substring(0, dot), key.Substring(dot + 1));
                int made = 0;
                for (int i = 0; i < n; i++)
                {
                    XYZ p = Architecte.PigerUneCase(b, r, 30);
                    if (p.Equals(XYZ.zero)) continue;
                    GameObject go;
                    ObjectPoolSingleton.Request(out go, ik, p.ToVector3() + Vector3.up * (0.6f + (i % 3) * 0.6f), Quaternion.Euler(0f, 90f * (i & 3), 0f));
                    if (go == null) continue;
                    UnityEngine.Networking.NetworkServer.Spawn(go, ik.AssetId);
                    go.GetOrAddComponent<AffiliationComponent>().faction = f;
                    made++;
                }
                BtcLog.Info("Stress", "items " + key + " " + made + "/" + n);
            }
            catch (Exception e) { BtcLog.Warn("Stress", "items: " + e.Message); }
        }
    }
}
#endif
