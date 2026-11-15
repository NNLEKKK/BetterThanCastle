using System;
using System.Collections.Generic;
using Brix.Assets;
using Brix.Engine;
using Brix.External.Factories;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.AI.KnowledgeSpace;
using Brix.Game;
using Brix.Game.Components;
using Brix.Game.Components.Tracking;
using Brix.Game.Semantique;
using Brix.Input;
using Brix.Lifecycle.Pooling;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Terraforming (Features/Terraform/BtcTerraform.cs): a Flatten task with a registered shape walks the shape cells
    // instead of its bounds and decides dig / fill per cell. Tasks without a shape stay pure vanilla (registry lookup only).

    [HarmonyPatch(typeof(LandscapeGoalProvider), "OnDataChange")]
    internal static class TerraformDataChangePatch
    {
        private static void Postfix(LandscapeGoalProvider __instance)
        {
            try
            {
                BtcTerraform.Shape s = BtcTerraform.Of(__instance);
                if (s != null) __instance.SetVolume(s.Vol);
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.OnDataChange: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoalProvider), "VoxelNeedsGoal")]
    internal static class TerraformVoxelNeedsGoalPatch
    {
        // same rules as vanilla Flatten, with "fill or dig" taken from the shape instead of the threshold height:
        // a fill goal only on the lowest cell that still needs filling (bottom-first), a dig goal only next to air
        private static bool Prefix(LandscapeGoalProvider __instance, XYZ voxel, ref SpawnOrder __result)
        {
            BtcTerraform.Shape s = BtcTerraform.Of(__instance);
            if (s == null) return true;
            try
            {
                __result = SpawnOrder.None;
                BtcTerraform.Ladder lad;
                if (s.Ladders.TryGetValue(voxel, out lad) && BtcTerraform.LadderReady(voxel, lad, s))
                {
                    __result = new SpawnOrder(voxel, lad.F, Test.BuildStairsGoal, false);
                    return false;
                }
                if (!__instance.NeedsAGoal(voxel)) return false;
                // like the quarry: never dig a ladder, the cell under one (its support) or a cell kept for a quarry ladder
                if (s.Ladders.ContainsKey(voxel + XYZ.up) || QuarryGoalProvider2.IsStair(voxel) || QuarryGoalProvider2.IsStair(voxel + XYZ.up)) return false;
                if (s.Fill.Contains(voxel))
                {
                    if (__instance.NeedsFill(voxel))
                    {
                        // a fill cell is a DigGoal set up to fill (TerraformFill): the Flatten fill goal reaches one block up only,
                        // the DigGoal two, so a mound is filled up to 2 above the worker's head like vanilla building
                        __instance.toFill.Add(voxel);
                        if (!__instance.NeedsFill(voxel - XYZ.up)) __result = new SpawnOrder(voxel, XYZ.zero, Test.DigGoal, false);
                    }
                }
                else if (s.Dig.Contains(voxel) && __instance.NeedsDig(voxel))
                {
                    // dig cells get the vanilla Tunnel / Quarry DigGoal: its access reaches far wider than a Flatten goal
                    // (a worker digs up to 3 high in a tunnel), so tunnels and staircases are dug from below
                    __instance.toDig.Add(voxel);
                    if (BtcTerraform.OpenToDig(voxel)) __result = new SpawnOrder(voxel, XYZ.zero, Test.DigGoal, false);
                }
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.VoxelNeedsGoal: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoalProvider), "CreateGoal")]
    internal static class TerraformCreateGoalPatch
    {
        // ladder cells get a vanilla BuildStairsGoal (like QuarryGoalProvider2.SpawnStairGoal), dig cells a DigGoal (like
        // QuarryGoalProvider2.SpawnDigGoal) whose resource setting carries the task's harvest toggles
        private static bool Prefix(LandscapeGoalProvider __instance, SpawnOrder order, ref Goal __result)
        {
            // the same cell queued twice (a local kick and the full re-walk): reuse the live goal instead of leaking one
            SpawnOrder have;
            if ((order.key == Test.DigGoal || order.key == Test.BuildStairsGoal) && BtcTerraform.Of(__instance) != null
                && __instance.spawnPerVoxel.TryGetValue(order.pos, out have) && have.key == order.key && !have.instance.IsNullOrReleased())
            { __result = have.instance; return false; }
            if (order.key == Test.DigGoal)
            {
                BtcTerraform.Shape s = BtcTerraform.Of(__instance);
                if (s == null) return true;
                try
                {
                    DigGoal d;
                    ObjectPoolSingleton.RequestComponent<DigGoal>(out d, Test.DigGoal, order.pos.ToVector3(), Quaternion.identity, true);
                    d.transform.SetParent(__instance.goalParent.transform);
                    d.ResourceSetting = new TerraformDigSetting(s, d.transform.parent, s.Fill.Contains(order.pos));
                    d.RebuildNode();
                    TerraformFill.Configure(d);
                    __result = d;
                    return false;
                }
                catch (Exception e) { BtcLog.Error("Hook", "Terraform.CreateDigGoal: " + e.Message); return true; }
            }
            if (order.key != Test.BuildStairsGoal) return true;
            try
            {
                BuildStairsGoal g;
                ObjectPoolSingleton.RequestComponent<BuildStairsGoal>(out g, Test.BuildStairsGoal, order.pos.ToVector3(), Quaternion.identity, true);
                g.transform.SetParent(__instance.goalParent.transform);
                g.transform.forward = order.direction.ToVector3();
                __result = g;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.CreateGoal: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoalProvider), "ReevaluateAllThresholds")]
    internal static class TerraformReevaluatePatch
    {
        private static readonly List<Goal> Tmp = new List<Goal>();

        // vanilla casts every goal to LandscapeGoal; a terraform task also holds BuildStairsGoal
        private static bool Prefix(LandscapeGoalProvider __instance)
        {
            if (BtcTerraform.Of(__instance) == null) return true;
            try
            {
                Tmp.Clear();
                __instance.GetAll(Tmp);
                for (int i = 0; i < Tmp.Count; i++)
                {
                    LandscapeGoal g = Tmp[i] as LandscapeGoal;
                    if (!g.IsNullOrReleased()) g.EvaluateTheshold(__instance.Threshold);
                }
                Tmp.Clear();
                return false;
            }
            catch (Exception e) { Tmp.Clear(); BtcLog.Error("Hook", "Terraform.Reevaluate: " + e.Message); return true; }
        }
    }

    /// <summary>Goal kinds of a terraform task: vanilla LandscapeProject.HasDigGoals / HasFillGoals call a method on
    /// "g as LandscapeGoal", which throws on a BuildStairsGoal, so a task with a shape counts its goals here.</summary>
    internal static class TerraformGoals
    {
        private static readonly List<Goal> Tmp = new List<Goal>();

        // kind: 0 dig, 1 fill, 2 ladder
        internal static bool Has(LandscapeGoalProvider p, int kind, IGameComponent target, bool reachableOnly)
        {
            Tmp.Clear();
            p.GetAll(Tmp);
            bool found = false;
            for (int i = 0; i < Tmp.Count && !found; i++)
            {
                Goal g = Tmp[i];
                if (g.IsNullOrReleased()) continue;
                LandscapeGoal lg = g as LandscapeGoal;
                bool fillDig = TerraformDigSetting.IsFill(g);
                bool match = kind == 2 ? g is BuildStairsGoal
                    : kind == 0 ? (g is DigGoal && !fillDig) || (lg != null && lg.GetLandscapeType() == LandscapeGoal.Landscape.Dig)
                    : fillDig || (lg != null && lg.GetLandscapeType() == LandscapeGoal.Landscape.Fill);
                if (match && (!reachableOnly || Knowledge.Instance.CanReach(g, target, -1))) found = true;
            }
            Tmp.Clear();
            return found;
        }
    }

    [HarmonyPatch(typeof(LandscapeProject), "HasFillGoals")]
    internal static class TerraformHasFillPatch
    {
        private static bool Prefix(LandscapeProject __instance, IGameComponent target, bool reacheableOnly, ref bool __result)
        {
            if (BtcTerraform.Of(__instance) == null) return true;
            try { __result = TerraformGoals.Has(__instance.goalProvider, 1, target, reacheableOnly); return false; }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.HasFillGoals: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeProject), "HasDigGoals")]
    internal static class TerraformHasDigPatch
    {
        private static bool Prefix(LandscapeProject __instance, IGameComponent target, bool reacheableOnly, ref bool __result)
        {
            if (BtcTerraform.Of(__instance) == null) return true;
            try { __result = TerraformGoals.Has(__instance.goalProvider, 0, target, reacheableOnly); return false; }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.HasDigGoals: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoal), "EvaluateTheshold")]
    internal static class TerraformGoalTypePatch
    {
        private static bool Prefix(LandscapeGoal __instance)
        {
            BtcTerraform.Shape s = BtcTerraform.Of(__instance._landscapeProject);
            if (s == null) return true;
            try
            {
                __instance.type = s.Fill.Contains(__instance.voxel.position) ? LandscapeGoal.Landscape.Fill : LandscapeGoal.Landscape.Dig;
                __instance.uniqueItemKeys.Clear();
                __instance.OnChangedType.Invoke(__instance);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.EvaluateTheshold: " + e.Message); return true; }
        }
    }

    /// <summary>Harvest toggles (Stone / Brimstone / Iron) of a terraform task, same meaning as the Tunnel / Quarry toggles:
    /// an unwanted resource is destroyed with the voxel (ForceOverkill), so it needs no bag room and no storage. Everything
    /// that is not brimstone or iron (stone, clay, plain earth) counts as stone, so with all toggles off nothing ever drops.</summary>
    internal static class TerraformHarvest
    {
        internal static int Flag(Factory.AssetKey key)
        {
            if (key == RawResources.RawOrangeCrystal) return 2;
            if (key == RawResources.RawIron) return 4;
            return 1;
        }

        internal static bool Overkill(BtcTerraform.Shape s, XYZ pos)
        {
            int f = Flag(Voxel.GetBlockInfo(pos).ResourceKey);
            return f != 0 && (s.Harvest & f) == 0;
        }

        internal static int InGround(XYZ pos)
        {
            return Voxel.Engine.GetTerrainResources(Voxel.Engine.GetVoxelHandle(pos)).TotalAmount;
        }

        internal static BtcTerraform.Shape DigShape(LandscapeGoal g)
        {
            if (g == null || !g.IsDig) return null;
            return BtcTerraform.Of(g._landscapeProject);
        }
    }

    [HarmonyPatch(typeof(LandscapeGoal), "UniqueItems")]
    internal static class TerraformUniqueItemsPatch
    {
        private static HashSet<Factory.AssetKey> Tmp = new HashSet<Factory.AssetKey>();

        private static bool Prefix(LandscapeGoal __instance, ref HashSet<Factory.AssetKey> keys)
        {
            BtcTerraform.Shape s = TerraformHarvest.DigShape(__instance);
            if (s == null || s.Harvest == 7) return true;
            try
            {
                Tmp.Clear();
                DigGoal.UniqueItem(__instance.voxel.position, ref Tmp);
                foreach (Factory.AssetKey k in Tmp)
                {
                    int f = TerraformHarvest.Flag(k);
                    if (f == 0 || (s.Harvest & f) != 0) keys.Add(k);
                }
                Tmp.Clear();
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.UniqueItems: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoal), "DigAction")]
    internal static class TerraformDigActionPatch
    {
        private static bool Prefix(LandscapeGoal __instance, Labor labor, ref StaticNode __result)
        {
            BtcTerraform.Shape s = TerraformHarvest.DigShape(__instance);
            if (s == null || s.Harvest == 7) return true;
            try
            {
                if (!TerraformHarvest.Overkill(s, __instance.voxel.position)) return true;
                DamageReaction r = __instance.damageReceiver.ReceiveAttack(60f, DamageType.Melee, DamageReaction.ForceOverkill, __instance.transform.position, __instance.transform.position, 0f, labor.gameObject);
                __result = r == DamageReaction.Failed ? Node.FailWork<Exceptions.VoxelHasBeenBuildOver>() : Node.Empty;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.DigAction: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoal), "IsMatchFor")]
    internal static class TerraformIsMatchPatch
    {
        // a terraform dig never needs a stockpile (vanilla demands one unless the task also fills): kept stone goes into
        // the bag and from there into the fill; only the bag room for a kept voxel is checked, a destroyed one needs none
        private static bool Prefix(LandscapeGoal __instance, Labor labor, AIErrorsStore errorDestination, ref float time, ref bool __result)
        {
            BtcTerraform.Shape s = TerraformHarvest.DigShape(__instance);
            if (s == null) return true;
            try
            {
                XYZ pos = __instance.voxel.position;
                __result = false;
                if (__instance.HasOtherWorkerAssigned(labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
                time += 0.05f;
                if (!TerraformReach.Can(labor, __instance)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CantReach>.Get.Problem, labor); return false; }
                if (!TerraformHarvest.Overkill(s, pos) && labor.recepteur.CanFitUpTo(RawResources.RawStone) < TerraformHarvest.InGround(pos))
                { errorDestination.AddDecisionError(SharedWorkException<Exceptions.BagTooFullToDig>.Get.Problem, labor); return false; }
                if (!labor.Occupation.Profession.CanBreakVoxels()) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CharacterCantDig>.Get.Problem, labor); return false; }
                time += 0.1f;
                __result = true;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.IsMatchFor: " + e.Message); return true; }
        }
    }

    /// <summary>Reach check of terraform goals: the knowledge regions catch up a while after a voxel is dug, so a cell that the
    /// worker just opened next to himself looked unreachable and he stood idle. A cell within arm's reach (the worker's
    /// neighbours, one up or down included) counts as reachable at once.</summary>
    internal static class TerraformReach
    {
        internal static bool Can(Labor labor, Goal g)
        {
            if (Knowledge.Instance.CanReach(labor, g, -1)) return true;
            Vector3 d = g.transform.position - labor.transform.position;
            if (Mathf.Abs(d.y) <= 2.6f && d.x * d.x + d.z * d.z <= 6.5f) return true;   // within arm's reach (DigGoal: 2 up)
            // a goal spawned a moment ago is not in the knowledge regions yet (KnowsAbout false = unknown, not unreachable):
            // close to the worker it counts as reachable, else he ran off to an old goal far away while the one he just
            // opened waited for the region update; a real dead end fails the walk and vanilla excludes it
            return !Knowledge.Instance.KnowsAbout(g.gameObject) && d.sqrMagnitude <= 36f;
        }
    }

    /// <summary>Priorities of terraform goals: vanilla ranks dig goals by height first (the whole top layer before anything
    /// else) and ladders below every dig, so workers walked round a hill to its top, or stood idle without a way up.
    /// Terraform: carrying stones -> fill until the bag is empty; empty bag -> the nearest dig or ladder; fill always
    /// bottom layer first, then the nearest.</summary>
    internal static class TerraformPriority
    {
        internal const int Inventory = 1 << 29;

        internal static int Of(Labor labor, Goal g, bool fill, int y)
        {
            // carrying stones, or stones lying near the worker: fill first (the fill picks the loose stones up on its way,
            // the Flatten task has no litter pick-up of its own); otherwise dig first
            int carried = labor.recepteur.ContentDescription.Value<Stones>();
            bool wantFill = carried > 0 || LooseStonesNear(labor);
            int bonus = (fill ? wantFill : !wantFill) ? Inventory : 0;
            int dist = Mathf.Min(Mathf.RoundToInt(Vector3.Distance(labor.transform.position, g.transform.position)), 4000);
            return fill ? bonus - Mathf.Clamp(y + 1100, 0, 4000) * 65536 / 16 - dist * 64 : bonus - dist * 1024;
        }

        private struct Loose { internal bool Near; internal float At; }
        private static readonly Dictionary<int, Loose> LooseCache = new Dictionary<int, Loose>();
        private const float LooseRange = 12f;

        // the stone the worker would take for a fill lies on the ground (not in a stockpile) close to him; cached for a second
        private static bool LooseStonesNear(Labor labor)
        {
            int id = labor.GetInstanceID();
            Loose l;
            float now = Time.time;
            if (LooseCache.TryGetValue(id, out l) && now - l.At < 1f) return l.Near;
            l.At = now;
            l.Near = NearestLoose(labor) != null;
            if (LooseCache.Count > 256) LooseCache.Clear();
            LooseCache[id] = l;
            return l.Near;
        }

        /// <summary>The nearest stone lying on the ground within 12 blocks of the worker (vanilla litter search: tracking
        /// layer SmallResources, on the ground, granular), or null.</summary>
        internal static GameObject NearestLoose(Labor labor)
        {
            Vector3 at = labor.transform.position;
            GameObject best = null;
            float bd = LooseRange * LooseRange;
            foreach (TrackingRegistry.TrackingEntry t in TrackingRegistry.GetCollisions(at, LooseRange, TrackingLayer.SmallResources, Faction.DefaultNeutral.AsList))
            {
                GameObject go = t.GameObject;
                if (go.IsNullOrReleased() || !Apparence.Is<Stones>(go) || !LitterGoalProvider.IsOnGround(go)) continue;
                float d = (go.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = go; }
            }
            return best;
        }
    }

    /// <summary>Resource setting of a terraform DigGoal: the task's harvest toggles. Also marks the goal as a terraform one (pooled
    /// DigGoals are reused by vanilla, so the goal counts only while it still hangs under the task's goal parent).</summary>
    internal sealed class TerraformDigSetting : IDigGoalResourceSetting
    {
        internal readonly BtcTerraform.Shape S;
        internal readonly bool Fill;   // this DigGoal fills its cell instead of digging it (TerraformFill)
        private readonly Transform _parent;

        internal TerraformDigSetting(BtcTerraform.Shape s, Transform parent, bool fill) { S = s; _parent = parent; Fill = fill; }

        public GroundResource TypesToHarvest { get { return (GroundResource)(S.Harvest & 7); } }

        internal static TerraformDigSetting Get(DigGoal g)
        {
            TerraformDigSetting t = g.ResourceSetting as TerraformDigSetting;
            return t != null && t._parent != null && g.transform.parent == t._parent ? t : null;
        }

        /// <summary>Shape of a terraform DIG goal (fill goals excluded).</summary>
        internal static BtcTerraform.Shape Of(DigGoal g)
        {
            TerraformDigSetting t = Get(g);
            return t != null && !t.Fill ? t.S : null;
        }

        internal static bool IsFill(Goal g)
        {
            DigGoal d = g as DigGoal;
            if (d == null) return false;
            TerraformDigSetting t = Get(d);
            return t != null && t.Fill;
        }
    }

    /// <summary>A terraform fill cell served by a DigGoal (wider reach than the Flatten fill goal): the work node of vanilla
    /// LandscapeGoal fill (pick stones up, then add earth worth 10 stones minus what the cell holds, take the stones from the bag).</summary>
    internal static class TerraformFill
    {
        internal static void Configure(DigGoal d)
        {
            TerraformDigSetting t = TerraformDigSetting.Get(d);
            if (t == null || !t.Fill || d.workNodeGenerator == null) return;
            WorkNodeGenerator<DigGoal> gen = d.workNodeGenerator;
            gen.ActionWrappers.Clear();
            gen.PrepareAction = (g, l) => Prepare(l);
            gen.WorkAction = (g, l) => Node.Sequence.Do(delegate(DigGoal goal, Labor labor) { Apply(goal, labor); }, g, l).Label("Terraform fill").ToNode;
            gen.WorkDoneCondition = (g, l) => Filled(g.voxel.position);
            gen.WorkInvalidCondition = (g, l) => g.IsNullOrReleased() || Filled(g.voxel.position);
            gen.ReleaseWhen = ReleaseNodeWhen.Ended | ReleaseNodeWhen.Invalid;
        }

        internal static bool Filled(XYZ p) { return !Voxel.IsEmpty(p) && !Voxel.IsCrust(p); }

        // stones lying close by first (picked up on the way), else vanilla: the bag or the nearest source
        private static Node Prepare(Labor l)
        {
            if (l.recepteur.ContentDescription.Value<Stones>() < 10)
            {
                GameObject loose = TerraformPriority.NearestLoose(l);
                if (loose != null && l.recepteur.HasRoomFor(loose))
                {
                    Brix.Game.Components.GameComponent gc = loose.GetComponent<Brix.Game.Components.GameComponent>();
                    if (gc != null) return Brix.Game.AI.Nodes.PickUpNode.GoPickUp(l, gc, 20);
                }
            }
            return Brix.Game.AI.Nodes.PickUpNode.FindAndPickUp(l, Adjectif.stones.Profile, 20);
        }

        private static void Apply(DigGoal goal, Labor labor)
        {
            XYZ p = goal.voxel.position;
            int qty = 10 - TerraformHarvest.InGround(p);
            if (qty <= 0) return;
            Brix.Game.Batchers.VoxelRequestBatcher.Instance.CallRpcAddTerrainResources(p.x, p.y, p.z, VoxelTerrainType.Sand, qty, true, true);
            int left = qty;
            List<GameObject> take = new List<GameObject>();
            foreach (var d in labor.recepteur.StoredItems)
            {
                if (left <= 0) break;
                if (d.Description.Value<Stones>() != 1) continue;
                take.Add(d.GameObject); left--;
            }
            for (int i = 0; i < take.Count; i++) take[i].SafeNetworkDestroy();
            BtcTerraform.Shape s = TerraformDigSetting.Get(goal).S;
            BtcTerraform.Kick(s, p);   // the cell above may take its goal now
        }
    }

    [HarmonyPatch(typeof(DigGoal), "SetupBuilder")]
    internal static class TerraformFillSetupPatch
    {
        // DigGoal.Start rebuilds its work node: keep the fill setup
        private static void Postfix(DigGoal __instance)
        {
            try { TerraformFill.Configure(__instance); }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.FillSetup: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(DigGoal), "IsMatchFor")]
    internal static class TerraformDigMatchPatch
    {
        // vanilla checks without the storage one (a terraform task never needs a stockpile); bag room only for a kept voxel
        private static bool Prefix(DigGoal __instance, Labor labor, AIErrorsStore errorDestination, ref float time, ref bool __result)
        {
            TerraformDigSetting t = TerraformDigSetting.Get(__instance);
            if (t == null) return true;
            try
            {
                XYZ pos = __instance.voxel.position;
                __result = false;
                if (__instance.HasOtherWorkerAssigned(labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
                time += 0.05f;
                if (t.Fill)
                {
                    // vanilla LandscapeGoal fill rules: a floor under the cell, stones the worker can get (bag or nearest)
                    if (Voxel.IsEmpty(pos - XYZ.up)) return false;
                    GameObject st = labor.Find(Adjectif.stones.Profile);
                    if (st == null || !labor.recepteur.HasRoomFor(st)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CannotFindWhatINeedToPickUp>.Get.Problem, labor); return false; }
                    time += 0.1f;
                    __result = true;
                    return false;
                }
                if (!labor.Occupation.Profession.CanBreakVoxels()) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CharacterCantDig>.Get.Problem, labor); return false; }
                // a full bag does not stop the dig (user 2026-11-15): with a fill to do the worker goes filling (priority),
                // without one he keeps digging; what does not fit stays on the ground for the fill
                time += 0.15f;
                __result = true;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.DigIsMatch: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(DigGoal), "PriorityFor")]
    internal static class TerraformDigPriorityPatch
    {
        // vanilla DigGoal: unreachable = int.MinValue, else height first; terraform: the nearest
        private static bool Prefix(DigGoal __instance, Labor labor, ref int __result)
        {
            TerraformDigSetting t = TerraformDigSetting.Get(__instance);
            if (t == null) return true;
            try
            {
                __result = TerraformReach.Can(labor, __instance) ? TerraformPriority.Of(labor, __instance, t.Fill, __instance.voxel.position.y) : int.MinValue;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.DigPriority: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(DigGoal), "ApplyDamage")]
    internal static class TerraformDigDamagePatch
    {
        // vanilla overkills only stone / brimstone / iron that is not kept; a terraform task destroys every resource that is
        // not kept (clay, plain earth count as stone), so with all toggles off nothing ever drops
        private static bool Prefix(DigGoal __instance, Labor labor)
        {
            BtcTerraform.Shape s = TerraformDigSetting.Of(__instance);
            if (s == null) return true;
            try
            {
                XYZ pos = __instance.voxel.position;
                DamageReaction expected = TerraformHarvest.Overkill(s, pos) ? DamageReaction.ForceOverkill : DamageReaction.Ignored;
                float value = Voxel.GetBlockInfo(pos).Type == BlockType.Terrain ? labor.Occupation.Profession.TerrainDamage : labor.Occupation.Profession.BlockBreaking;
                DamageReaction r = __instance.damageReceiver.ReceiveAttack(value, DamageType.Siege, expected, Vector3.zero, Vector3.zero, 0f, labor.gameObject);
                // the voxel is gone: re-walk the task at once so the cells it opened get their goals before the worker picks again
                if (r == DamageReaction.Die || r == DamageReaction.ForceOverkill) BtcTerraform.Kick(s, pos);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.DigDamage: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeGoal), "PriorityFor")]
    internal static class TerraformPriorityPatch
    {
        private static bool Prefix(LandscapeGoal __instance, Labor labor, ref int __result)
        {
            if (BtcTerraform.Of(__instance._landscapeProject) == null) return true;
            try { __result = TerraformPriority.Of(labor, __instance, __instance.IsFill, __instance.voxel.position.y); return false; }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.PriorityFor: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "SpawnStairs")]
    internal static class TerraformStairBuiltPatch
    {
        // a ladder is in: the cells it unlocks (the block over it, the next ramp) get their goals at once
        private static void Postfix(BuildStairsGoal __instance)
        {
            if (BtcTerraform.ByObj.Count == 0) return;
            try { XYZ p = __instance.voxel.position; BtcTerraform.Shape s = BtcTerraform.LadderShape(p); if (s != null) BtcTerraform.Kick(s, p); }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.StairBuilt: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "PriorityFor")]
    internal static class TerraformStairPriorityPatch
    {
        private static bool Prefix(BuildStairsGoal __instance, Labor labor, ref int __result)
        {
            if (BtcTerraform.ByObj.Count == 0) return true;
            try
            {
                if (!BtcTerraform.IsLadderCell(__instance.voxel.position)) return true;
                __result = TerraformPriority.Of(labor, __instance, false, 0);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.StairPriority: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "IsMatchFor")]
    internal static class TerraformStairMatchPatch
    {
        // vanilla checks with the near-reach rule (see TerraformReach)
        private static bool Prefix(BuildStairsGoal __instance, Labor labor, AIErrorsStore errorDestination, ref float time, ref bool __result)
        {
            if (BtcTerraform.ByObj.Count == 0) return true;
            try
            {
                if (!BtcTerraform.IsLadderCell(__instance.voxel.position)) return true;
                __result = false;
                if (__instance.HasOtherWorkerAssigned(labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
                time += 0.05f;
                if (!TerraformReach.Can(labor, __instance)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CantReach>.Get.Problem, labor); return false; }
                time += 0.05f;
                if (!labor.Profession.CanBreakVoxels()) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CharacterCantDig>.Get.Problem, labor); return false; }
                __result = true;
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.StairIsMatch: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeProject), "HasWorkFor")]
    internal static class TerraformHasWorkPatch
    {
        // vanilla rule plus ladder goals (they need nothing). Digging needs bag room only for kept stone (room for a whole
        // voxel), so a full bag with nothing left to fill has no work: the worker leaves the task and stores the stone itself
        private static bool Prefix(LandscapeProject __instance, Labor labor, ref bool __result)
        {
            BtcTerraform.Shape s = BtcTerraform.Of(__instance);
            if (s == null) return true;
            try
            {
                if (s.Ladders.Count > 0 && TerraformGoals.Has(__instance.goalProvider, 2, labor, false)) { __result = true; return false; }
                bool room = true;   // a full bag keeps digging (see TerraformDigMatchPatch)
                int digLeft, allLeft;
                BtcTerraform.Pending(s, out digLeft, out allLeft);
                // cells still to dig keep the crew even while no goal is spawned yet (a crust is being removed, the next layer
                // opens up): vanilla would let every worker go and then auto-delete the task
                __result = (room && __instance.HasRoomFor(labor) && (digLeft > 0 || __instance.HasDigGoals(labor, false)))
                    || (__instance.HasFillGoals(labor, false) && labor.Find(Adjectif.stones.Profile));
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.HasWorkFor: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(BuildStairsGoal), "IsValid")]
    internal static class TerraformStairValidPatch
    {
        // vanilla wants the cell above a ladder empty although building it removes a crust there (RemoveCroute): a terraform
        // ladder under a crust, or under a block the task digs afterwards from the ramp, would never be picked by a worker
        private static void Postfix(BuildStairsGoal __instance, ref bool __result)
        {
            if (__result || BtcTerraform.ByObj.Count == 0) return;
            try
            {
                XYZ p = __instance.voxel.position, up = p + XYZ.up;
                BtcTerraform.Shape s = BtcTerraform.LadderShape(p);
                if (s != null && (Voxel.IsCrust(up) || s.Dig.Contains(up))) __result = true;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.StairIsValid: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(LandscapeProject), "CheckForAutoDelete")]
    internal static class TerraformAutoDeletePatch
    {
        // vanilla deletes a Flatten whose crew left while it had no goal for a moment; a terraform task stays until its cells are done
        private static bool Prefix(LandscapeProject __instance)
        {
            BtcTerraform.Shape s = BtcTerraform.Of(__instance);
            if (s == null) return true;
            try { return BtcTerraform.Left(s) == 0; }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.AutoDelete: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(LandscapeProject), "VerifyWorkFor")]
    internal static class TerraformVerifyWorkPatch
    {
        // a terraform task never sends its workers to storage (vanilla skips the check only when the task both digs and fills)
        private static bool Prefix(LandscapeProject __instance, ref Node __result)
        {
            if (BtcTerraform.Of(__instance) == null) return true;
            __result = Node.Empty;
            return false;
        }
    }

    // the terraform tool owns the mouse while it is open: no vanilla picking (selection, orders, hover)
    [HarmonyPatch(typeof(Picking), "OnUpdate")]
    internal static class TerraformPickingPatch
    {
        private static bool Prefix() { return !BtcTerraformTool.Active; }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_Save")]
    internal static class TerraformDiskSavePatch
    {
        private static void Postfix(Asset_Map __instance)
        {
            try { BtcTerraform.OnSave(__instance.path); }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.Save: " + e); }
        }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_ReadGameObjects")]
    internal static class TerraformDiskReadPatch
    {
        private static void Prefix(Asset_Map __instance)
        {
            try { BtcTerraform.OnLoad(__instance.path); }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.Load: " + e); }
        }
    }
}
