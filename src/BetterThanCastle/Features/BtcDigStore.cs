using System.Collections.Generic;
using Brix.Game.AI;
using Brix.Game.AI.InstructionType;
using Brix.Game.AI.Nodes;
using Brix.Game.Semantique;
using Brix.UI.Icons;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle
{
    /// Rule "work.digStore" (host rule): a worker who takes automatic digging work (quarry, tunnel, flatten / terraform) with a big item in the hands
    /// (ingot, brick, plank, kit part: anything that does not go into the bag) first puts it into a fitting storage, like the bear traps
    /// task does. Bag contents (stones, ores) stay. No fitting storage = vanilla (digs with the item).
    internal static class BtcDigStore
    {
        private static int _idx = -2;

        internal static void AddRows()
        {
            BtcGameplay.Add("work.digStore", "eco", "Digging: store big items first", "Копание: сначала убрать большие предметы", 1f, 0f, 1f, 1f, true,
                "A worker who goes to quarry, tunnel, flatten or terraforming work with a big item in the hands (ingot, brick, plank, kit part) first puts it into a fitting storage, then digs. What is in the bag stays. Without a fitting storage it works as in vanilla.",
                "Рабочий, который идёт копать (карьер, туннель, выравнивание, терраформинг) с большим предметом в руках (слиток, кирпич, доска, часть набора), сначала кладёт его на подходящий склад и только потом копает. Содержимое мешка остаётся. Если подходящего склада нет, всё как в ванили.");
        }

        internal static bool On
        {
            get
            {
                if (_idx == -2) _idx = BtcGameplay.Idx("work.digStore");
                float[] a = BtcGameplay.Active;
                return _idx >= 0 && a != null && _idx < a.Length && a[_idx] > 0.5f;
            }
        }

        // LandscapeProject = vanilla Flatten and the mod terraform task (BtcTerraform)
        internal static bool IsDig(Project p) { return p is MineProject || p is TunnelProject || p is LandscapeProject; }

        /// something in the hands that is not bag content (granular)
        internal static bool CarriesBig(Labor l)
        {
            Recepteur r = l != null ? l.recepteur : null;
            if (r == null || r.IsEmpty()) return false;
            foreach (IDescriptor d in r.StoredItems) if (d != null && d.Description != null && !d.Description.Has<Granulaire>()) return true;
            return false;
        }

        private struct StoreAt { internal bool Ok; internal float At; }
        private static readonly Dictionary<int, StoreAt> _cache = new Dictionary<int, StoreAt>();

        // a fitting storage exists (cached 2 s per worker)
        private static bool CanStore(Labor l)
        {
            float now = Time.unscaledTime;
            int id = l.GetInstanceID();
            StoreAt s;
            if (_cache.TryGetValue(id, out s) && now - s.At < 2f) return s.Ok;
            s.Ok = l.BestRecepteurToStoreInventory() != null;
            s.At = now;
            if (_cache.Count > 512) _cache.Clear();
            _cache[id] = s;
            return s.Ok;
        }

        private static readonly IconKey StoreIcon = IconKeys.BricktronDropInContainer;

        /// Project.GetInstruction prefix body: true = the store package was handed out instead of a dig goal
        internal static bool TryStoreFirst(Project task, Labor labor, Var<OrderPackage<Labor>> result)
        {
            if (!On || labor == null || !IsDig(task) || !task.ReadyToAssignGoals() || !CarriesBig(labor) || !CanStore(labor)) return false;
            WorkInstruction<Labor> wi = new WorkInstruction<Labor>("Store before digging", StoreIcon, l => l.GoStoreInBestStorage());
            OrderPackage<Labor> pk = Order.Package.Of<Labor>(wi, Labor.DecisionSource.Project);
            pk.SetPrepare(delegate(BaseLabor bl) { return task.Hire(bl as Labor); });
            result.Set(pk);
#if DEV
            BtcLog.Info("DigStore", labor.name + " stores its load before digging at " + task.name);
#endif
            return true;
        }
    }
}
