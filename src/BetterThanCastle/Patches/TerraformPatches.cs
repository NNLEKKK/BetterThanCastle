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
                if (TerraformCrowd.Busy(__instance, labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
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

    /// <summary>Vanilla Goal.HasOtherWorkerAssigned without stale entries: a DigGoal is pooled and its private workers list is
    /// never cleared, so when the voxel died under a worker (Kick re-walks the task at once) the reused goal of the next cell
    /// still listed him; nobody worked on it and everybody got "enough workers doing it" (lab terraform-tunnel 2026-11-15:
    /// the upper block of a 1x2 tunnel waited 20 s with every worker idle).</summary>
    internal static class TerraformCrowd
    {
        private static readonly System.Reflection.FieldInfo Workers = AccessTools.Field(typeof(Goal), "workers");

        internal static bool Busy(Goal g, Labor labor)
        {
            if (!g.HasOtherWorkerAssigned(labor)) return false;
            List<Labor> w = Workers == null ? null : Workers.GetValue(g) as List<Labor>;
            if (w == null) return true;
            for (int i = w.Count - 1; i >= 0; i--)
            {
                Labor l = w[i];
                if (l == null || l.Goal != g) w.RemoveAt(i);
            }
            return g.HasOtherWorkerAssigned(labor);
        }
    }

    /// <summary>Reach check of terraform goals: the knowledge regions catch up a while after a voxel is dug, so a cell that the
    /// worker just opened next to himself looked unreachable and he stood idle. A cell within arm's reach (the worker's
    /// neighbours, one up or down included) counts as reachable at once.</summary>
    internal static class TerraformReach
    {
        // worker id + goal cell -> time of his last failed walk to it
        private static readonly Dictionary<long, float> Failed = new Dictionary<long, float>();
        private const float FailedFor = 30f;

        private static long Key(Labor labor, XYZ p)
        {
            return ((long)labor.GetInstanceID() << 32) ^ (uint)((p.x & 0x7FF) | ((p.y & 0x3FF) << 11) | ((p.z & 0x7FF) << 21));
        }

        /// <summary>The worker's walk to this goal failed: for a while only the knowledge space decides (no near / unknown
        /// shortcut). Else a cell he cannot stand next to (a fill 3 above him) stayed "close and unknown", he took it again
        /// every few seconds, stood still and blocked it for everybody (lab terraform-stuck 2026-11-15).</summary>
        internal static void MarkFailed(Labor labor, Goal g)
        {
            if (Failed.Count > 512)
            {
                // drop only expired marks: clearing all of them made every stuck worker pick his blocked cell again at once
                List<long> old = new List<long>();
                foreach (KeyValuePair<long, float> kv in Failed) if (Time.time - kv.Value >= FailedFor) old.Add(kv.Key);
                for (int i = 0; i < old.Count; i++) Failed.Remove(old[i]);
                if (Failed.Count > 4096) Failed.Clear();   // hard cap, unreachable in practice
            }
            Failed[Key(labor, g.voxel.position)] = Time.time;
        }

        internal static bool Can(Labor labor, Goal g)
        {
            float at;
            if (Failed.TryGetValue(Key(labor, g.voxel.position), out at) && Time.time - at < FailedFor) return Knowledge.Instance.CanReach(labor, g, -1);
            Vector3 d = g.transform.position - labor.transform.position;
            XYZ under;
            if (Capped(g, out under))
            {
                // a marked block still sits on this cell (1x2 tunnel: the lower block of the next column): only from the next
                // column, or from an opening with head room; else a second worker crawled through the 1-high hole left by the
                // lower block and dug a 1-high tunnel into the hill while the upper blocks stayed (user 2026-11-15)
                if (Mathf.Abs(d.y) <= 2.6f && d.x * d.x + d.z * d.z <= 2.25f) return true;
                return HeadRoom(under) && Knowledge.Instance.CanReach(labor, g, -1);
            }
            if (Knowledge.Instance.CanReach(labor, g, -1)) return true;
            if (Mathf.Abs(d.y) <= 2.6f && d.x * d.x + d.z * d.z <= 6.5f) return true;   // within arm's reach (DigGoal: 2 up)
            // a goal spawned a moment ago is not in the knowledge regions yet (KnowsAbout false = unknown, not unreachable):
            // close to the worker it counts as reachable, else he ran off to an old goal far away while the one he just
            // opened waited for the region update; a real dead end fails the walk and vanilla excludes it
            return !Knowledge.Instance.KnowsAbout(g.gameObject) && d.sqrMagnitude <= 36f;
        }

        /// <summary>A terraform dig cell with a marked block still to dig right above it (the upper block goes first).</summary>
        internal static bool Capped(Goal g, out XYZ p)
        {
            p = XYZ.zero;
            DigGoal dg = g as DigGoal;
            if (dg == null) return false;
            BtcTerraform.Shape s = TerraformDigSetting.Of(dg);
            if (s == null) return false;
            p = dg.voxel.position;
            XYZ up = p + XYZ.up;
            return s.Dig.Contains(up) && !Voxel.IsEmpty(up);
        }

        private static readonly XYZ[] Side = { new XYZ(1, 0, 0), new XYZ(-1, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 0, -1) };

        // a side neighbour a bricktron (2 high) can stand in
        private static bool HeadRoom(XYZ p)
        {
            for (int i = 0; i < Side.Length; i++) { XYZ n = p + Side[i]; if (Voxel.IsEmpty(n) && Voxel.IsEmpty(n + XYZ.up)) return true; }
            return false;
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
            float far = Mathf.Min(Vector3.Distance(labor.transform.position, g.transform.position), 4000f);
            if (fill) return bonus - Mathf.Clamp(y + 1100, 0, 4000) * 65536 / 16 - Mathf.RoundToInt(far) * 64;
            // dig: the nearest (exact distance: the upper and the lower block of a tunnel column tied when rounded), a cell
            // under a marked block still to dig 3 blocks later (head room first, a 2-high tunnel is walked into, not crawled)
            XYZ p;
            return bonus - Mathf.RoundToInt(far * 1024f) - (TerraformReach.Capped(g, out p) ? 3 * 1024 : 0);
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
            if (g == null) return null;
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
            gen.WorkAction = (g, l) => Node.Sequence.Do((DigGoal goal, Labor labor) => Hit(goal, labor), g, l).Label("Terraform fill").ToNode;
            gen.WorkDoneCondition = (g, l) => Filled(g.voxel.position);
            gen.WorkInvalidCondition = (g, l) => g.IsNullOrReleased() || Filled(g.voxel.position);
            gen.ReleaseWhen = ReleaseNodeWhen.Ended | ReleaseNodeWhen.Invalid;
        }

        internal static bool Filled(XYZ p) { return !Voxel.IsEmpty(p) && !Voxel.IsCrust(p); }

        /// <summary>A bricktron stands in the cell (feet in p, or feet under it and the head in p). Vanilla fills it anyway and
        /// its BuryBehavior then moves only the transform out of the earth, the locomotion stays in the old cell: the unit hangs
        /// for good and answers no order (same as vanilla Flatten). Such a cell waits until it is free.</summary>
        internal static bool Occupied(XYZ p, Labor labor)
        {
            if (VoxelPresence.HasPresenceAt(p) || VoxelPresence.HasPresenceAt(p - XYZ.up)) return true;
            if (labor != null && In(labor.navigation.Locomotion, labor.transform, p)) return true;
            // the presence registry knows only the cell a unit stands in: one walking over a 1-high fill row is entering it
            // (its feet end up in the earth, BuryBehavior only frees a buried head, the unit hangs for good)
            int n = Physics.OverlapSphereNonAlloc(p.ToVector3(), 1.6f, Hits, ~0, QueryTriggerInteraction.Ignore);
            bool hit = false;
            Bounds cell = new Bounds(p.ToVector3(), new Vector3(0.9f, 0.9f, 0.9f));
            for (int i = 0; i < n && !hit; i++)
            {
                Collider c = Hits[i];
                if (c == null) continue;
                Locomotion4 loc = c.GetComponentInParent<Locomotion4>();
                if (loc == null) continue;
                hit = c.bounds.Intersects(cell) || In(loc, loc.transform, p);
            }
            Array.Clear(Hits, 0, n);
            return hit;
        }

        private static readonly Collider[] Hits = new Collider[64];

        // the unit stands in or enters p, or into the cell under it (head in p)
        private static bool In(Locomotion4 loc, Transform t, XYZ p)
        {
            XYZ low = p - XYZ.up;
            XYZ feet = XYZ.FromVector3(t.position);
            if (feet == p || feet == low) return true;
            if (loc == null) return false;
            XYZ a = loc.PositionOccupied, b = loc.PositionEntering;
            return a == p || a == low || b == p || b == low;
        }

        // one tool hit: never into a unit; a cell that takes no earth (holds 10 already but is still crust) ends the work
        // instead of looping the animation for nothing
        private static Node Hit(DigGoal goal, Labor labor)
        {
            XYZ p = goal.voxel.position;
            if (Occupied(p, labor))
            {
#if DEV
                BtcLog.Info("TfFill", labor.name + " skip occupied " + p);
#endif
                return Node.FailWork<Exceptions.GoalTooCrowded>();
            }
            if (10 - TerraformHarvest.InGround(p) <= 0) return Node.FailWork<Exceptions.VoxelHasBeenBuildOver>();
            Apply(goal, labor);
            return Node.Empty;
        }

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
                if (TerraformCrowd.Busy(__instance, labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
                time += 0.05f;
                if (t.Fill)
                {
                    // vanilla LandscapeGoal fill rules: a floor under the cell, stones the worker can get (bag or nearest)
                    if (Voxel.IsEmpty(pos - XYZ.up)) return false;
                    if (TerraformFill.Occupied(pos, labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
                    if (!TerraformReach.Can(labor, __instance)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CantReach>.Get.Problem, labor); return false; }
                    GameObject st = labor.Find(Adjectif.stones.Profile);
                    if (st == null || !labor.recepteur.HasRoomFor(st)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CannotFindWhatINeedToPickUp>.Get.Problem, labor); return false; }
                    time += 0.1f;
                    __result = true;
                    return false;
                }
                if (!labor.Occupation.Profession.CanBreakVoxels()) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.CharacterCantDig>.Get.Problem, labor); return false; }
                // a full bag does not stop the dig (user 2026-11-15): with a fill to do the worker goes filling (priority),
                // without one he keeps digging; what does not fit stays on the ground for the fill
                BtcTerraform.Shape sh = t.S;
                if (sh.StoreFull && !TerraformHarvest.Overkill(sh, pos) && labor.recepteur.CanFitUpTo(RawResources.RawStone) < TerraformHarvest.InGround(pos))
                { errorDestination.AddDecisionError(SharedWorkException<Exceptions.BagTooFullToDig>.Get.Problem, labor); return false; }
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
            bool hit = false;   // once the damage is applied vanilla must not run (it would hit the cell a second time)
            try
            {
                XYZ pos = __instance.voxel.position;
                DamageReaction expected = TerraformHarvest.Overkill(s, pos) ? DamageReaction.ForceOverkill : DamageReaction.Ignored;
                float value = Voxel.GetBlockInfo(pos).Type == BlockType.Terrain ? labor.Occupation.Profession.TerrainDamage : labor.Occupation.Profession.BlockBreaking;
                DamageReaction r = __instance.damageReceiver.ReceiveAttack(value, DamageType.Siege, expected, Vector3.zero, Vector3.zero, 0f, labor.gameObject);
                hit = true;
                // the voxel is gone: re-walk the task at once so the cells it opened get their goals before the worker picks again
                if (r == DamageReaction.Die || r == DamageReaction.ForceOverkill) BtcTerraform.Kick(s, pos);
                return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.DigDamage: " + e.Message); return !hit; }
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
                if (TerraformCrowd.Busy(__instance, labor)) { errorDestination.AddDecisionError(SharedWorkException<Exceptions.GoalTooCrowded>.Get.Problem, labor); return false; }
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
                // a full bag keeps digging (see TerraformDigMatchPatch) unless the task hauls to a stockpile: then the worker leaves
                // (vanilla stores the stone) and comes back
                bool room = !(s.StoreFull && s.Harvest != 0 && labor.recepteur.CanFitUpTo(RawResources.RawStone) < 10);
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

    [HarmonyPatch(typeof(Labor), "HandleWorkException")]
    internal static class TerraformWorkFailPatch
    {
        // a failed walk to a terraform goal (path failed / cannot reach): TerraformReach stops guessing for that worker and cell
        private static void Prefix(Labor __instance, Exceptions.WorkFailedException exception)
        {
            if (BtcTerraform.ByObj.Count == 0) return;
            try
            {
                if (!(exception is Exceptions.PathException) && !(exception is Exceptions.CantReach)) return;
                Goal g = __instance.Goal;
                if (g == null) return;
                if (TerraformDigSetting.Get(g as DigGoal) == null && !(g is BuildStairsGoal && BtcTerraform.IsLadderCell(g.voxel.position))) return;
                TerraformReach.MarkFailed(__instance, g);
            }
            catch (Exception e) { BtcLog.Error("Hook", "Terraform.WorkFail: " + e.Message); }
        }
    }
}
