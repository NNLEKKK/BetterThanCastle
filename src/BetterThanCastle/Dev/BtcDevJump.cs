#if DEV
using System;
using System.Collections.Generic;
using System.Text;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Network;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV group jump-off lab. "jumpsetup N": 4-voxel step next to the local units, N bricktrons crowded on the top.
    /// "jumpgo on|off": mov.jumpLine forced, every lab unit gets TurnAndJumpToward the SAME point below (worst case).
    /// "jumpdump tag": logs every unit voxel and the number of distinct landing voxels.</summary>
    internal static class BtcDevJump
    {
        private static readonly List<Labor> Units = new List<Labor>();
        private static int _ax, _az, _top;

        internal static void Setup(int n)
        {
            try
            {
                Faction f = User.LocalUser.faction;
                Vector3 c = Vector3.zero; int k = 0;
                foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == f) { c += l.transform.position; k++; }
                if (k == 0) { BtcLog.Warn("JumpLab", "no local units"); return; }
                c /= k;
                _ax = Mathf.RoundToInt(c.x) + 12; _az = Mathf.RoundToInt(c.z);
                int maxTop = 0;
                for (int x = _ax - 8; x <= _ax + 8; x++)
                    for (int z = _az - 8; z <= _az + 8; z++)
                        if (!VoxelEngine.IsEmptyColumn(x, z)) maxTop = Mathf.Max(maxTop, VoxelEngine.GetColumnTop(x, z));
                _top = Mathf.Min(maxTop + 3, 240);
                VoxelEngine.BeginChanges();
                for (int x = _ax - 8; x <= _ax + 8; x++)
                    for (int z = _az - 8; z <= _az + 8; z++)
                    {
                        VoxelEngine.SetColumnTopVoxel(x, z, x <= _ax ? _top : _top - 4);
                        VoxelEngine.SetColumnType(x, z, VoxelTerrainType.Pavement);
                        VoxelEngine.SetColumnCrustVolume(x, z, 1f);
                    }
                VoxelEngine.ApplyChanges(false);
                for (int x = _ax - 8; x <= _ax + 8; x++)
                    for (int z = _az - 8; z <= _az + 8; z++)
                        for (int y = _top - 5; y <= _top + 2; y++) Fire(x, y, z);
                Units.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector3 pos = new Vector3(_ax - 1.5f + (i % 2) * 0.2f, _top + 1.2f, _az + 0.5f + (i / 2) * 0.1f);
                    GameObject go = BtcDuel.SpawnUnit(BtcDuelUnit.Worker, f, pos, Quaternion.identity);
                    Labor l = go != null ? go.GetComponent<Labor>() : null;
                    if (l != null) Units.Add(l);
                }
                BtcLog.Info("JumpLab", "setup ax=" + _ax + " az=" + _az + " top=" + _top + " units=" + Units.Count);
            }
            catch (Exception e) { BtcLog.Error("JumpLab", "setup failed", e); }
        }

        private static void Fire(int x, int y, int z)
        {
            if (y < 0 || y > 254) return;
            XYZ p = new XYZ(x, y, z);
            GameSignals.Invoke<XYZ>(GameSignals.VoxelChanged, p);
            GameSignals.Invoke<XYZ>(GameSignals.TerrainChanged, p);
            Architecte.Changement(p);
        }

        internal static void Go(string mode)
        {
            try
            {
                int i = BtcGameplay.Idx("mov.jumpLine");
                if (i >= 0) BtcGameplay.Active[i] = mode == "off" ? 0f : 1f;
                Vector3 target = new Vector3(_ax + 4.5f, _top - 3f, _az + 0.5f);
                int sent = 0;
                foreach (Labor l in Units)
                {
                    if (l == null) continue;
                    Order.Move.TurnAndJumpToward.Send(l, Location.Of(target), Labor.DecisionSource.User);
                    sent++;
                }
                BtcLog.Info("JumpLab", "go " + mode + " sent=" + sent + " target=" + target);
            }
            catch (Exception e) { BtcLog.Error("JumpLab", "go failed", e); }
        }

        internal static void Dump(string tag)
        {
            HashSet<XYZ> seen = new HashSet<XYZ>();
            StringBuilder sb = new StringBuilder();
            int alive = 0, low = 0;
            foreach (Labor l in Units)
            {
                if (l == null) continue;
                XYZ v = l.presence.CurrentVoxel;
                alive++;
                if (v.y < _top) low++;
                seen.Add(v);
                sb.Append(' ').Append(v.x).Append(',').Append(v.y).Append(',').Append(v.z);
            }
            BtcLog.Info("JumpLab", "RESULT " + tag + " units=" + alive + " jumpedDown=" + low + " distinct=" + seen.Count + " voxels=" + sb);
        }

        internal static void Reset()
        {
            foreach (Labor l in Units) if (l != null) UnityEngine.Object.Destroy(l.gameObject);
            Units.Clear();
        }
    }
}
#endif
