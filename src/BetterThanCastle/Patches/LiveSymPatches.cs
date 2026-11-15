using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Assets;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.Network;
using Brix.Input.WorldEditor;
using Brix.Legacy;
using Brix.Lua;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Map editor live symmetry hooks (BtcLiveSym). All gated by editor mode + symmetry mode + user action.</summary>
    [HarmonyPatch(typeof(UNetBlueprint), "CmdSpawnPlacedBlueprint")]
    internal static class LiveSymBlueprintPatch
    {
        private static void Prefix(UNetBlueprint __instance, string factory, string name, Vector3 position, Quaternion rotation, bool instant)
        {
            try
            {
                int m, sx, sz;
                if (!BtcLiveSym.Active(out m, out sx, out sz)) return;
                BtcLiveSym.ForEachImage(m, sx, sz, position.x, position.z, (x, z, fx, fz) =>
                    __instance.CmdSpawnPlacedBlueprint(factory, name, new Vector3(x, position.y, z), BtcLiveSym.Rot(rotation, fx, fz), instant, null, false));
            }
            catch (Exception e) { BtcLog.Error("Hook", "LiveSym blueprint: " + e); }
        }
    }

    [HarmonyPatch(typeof(LuaFactory), "InstantiateFromCLR")]
    internal static class LiveSymFactoryPatch
    {
        private static void Postfix(Factory.AssetKey key, Faction faction, Vector3 position, Quaternion rotation, LuaDriver __result)
        {
            try
            {
                BtcEdUndo.Spawned(__result);
                int m, sx, sz;
                if (!BtcLiveSym.Active(out m, out sx, out sz)) return;
                BtcLiveSym.ForEachImage(m, sx, sz, position.x, position.z, (x, z, fx, fz) =>
                    LuaFactory.InstantiateFromCLR(key, faction, new Vector3(x, position.y, z), BtcLiveSym.Rot(rotation, fx, fz)));
            }
            catch (Exception e) { BtcLog.Error("Hook", "LiveSym factory: " + e); }
        }
    }

    // Deleting a placed object also deletes its mirror image (same name within 0.75 voxel of the image).
    [HarmonyPatch(typeof(LuaDriver), "Remove")]
    internal static class LiveSymRemovePatch
    {
        private static void Prefix(LuaDriver __instance)
        {
            try
            {
                int m, sx, sz;
                if (!BtcLiveSym.Active(out m, out sx, out sz)) return;
                Vector3 p = __instance.transform.position;
                string nm = __instance.gameObject.name;
                LuaDriver[] all = null;
                BtcLiveSym.ForEachImage(m, sx, sz, p.x, p.z, (x, z, fx, fz) =>
                {
                    if (all == null) all = UnityEngine.Object.FindObjectsOfType<LuaDriver>();
                    for (int i = 0; i < all.Length; i++)
                    {
                        LuaDriver d = all[i];
                        if (d == null || d == __instance || d.gameObject.name != nm) continue;
                        Vector3 q = d.transform.position;
                        if ((q.x - x) * (q.x - x) + (q.z - z) * (q.z - z) < 0.5625f && Mathf.Abs(q.y - p.y) < 3f)
                        {
                            d.Remove();
                            break;
                        }
                    }
                });
            }
            catch (Exception e) { BtcLog.Error("Hook", "LiveSym remove: " + e); }
        }
    }

    [HarmonyPatch(typeof(BrushUtils), "GrowTree")]
    internal static class LiveSymTreePatch
    {
        private static void Prefix(XYZ at) { BtcEdUndo.Tree(at); }

        private static void Postfix(XYZ at, float fraction, bool __result)
        {
            try
            {
                if (!__result) return;
                int m, sx, sz;
                if (!BtcLiveSym.Active(out m, out sx, out sz)) return;
                int info = TreeRegistry.Instance.GetTree(at);
                BtcLiveSym.ForEachImage(m, sx, sz, at.x, at.z, (x, z, fx, fz) =>
                {
                    XYZ p = new XYZ(Mathf.RoundToInt(x), at.y, Mathf.RoundToInt(z));
                    BtcEdUndo.Tree(p);
                    // same tree type and rotation index as the original (the vanilla pick depends on the position hash)
                    if (TreeRegistry.Instance.GetTree(p) == 0) TreeRegistry.Instance.Add(p, info & 15);
                    TreeRegistry.Instance.ModifyTree(p, info);
                });
            }
            catch (Exception e) { BtcLog.Error("Hook", "LiveSym tree: " + e); }
        }
    }

    // Nature props (iron, pyrite, boulders, plants) and tree removal: same call with mirrored coordinates.
    [HarmonyPatch]
    internal static class LiveSymNaturePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(IronRegistry), typeof(PyriteRegistry), typeof(PlantRegistry), typeof(BoulderRegistry) };
            string[] names = { "AddOrGrow", "RemoveOrUngrow", "Remove" };
            foreach (Type t in types)
                foreach (string n in names)
                    foreach (MethodInfo mi in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (mi.Name == n && mi.GetParameters().Length > 0) yield return mi;
            yield return AccessTools.Method(typeof(BrushUtils), "RemoveTree");
        }

        private static void Prefix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                if (__args.Length == 0 || !(__args[0] is XYZ)) return;
                XYZ p = (XYZ)__args[0];
                Type t = __originalMethod.DeclaringType;
                if (t == typeof(IronRegistry)) BtcEdUndo.Mineral(0, p);
                else if (t == typeof(PyriteRegistry)) BtcEdUndo.Mineral(1, p);
                else if (t == typeof(PlantRegistry) && __originalMethod.Name == "AddOrGrow") BtcEdUndo.PlantAdd(p);
                else if (__originalMethod.Name == "RemoveTree") BtcEdUndo.Tree(p);
            }
            catch (Exception e) { BtcLog.Error("Undo", "nature capture: " + e); }
        }

        private static void Postfix(MethodBase __originalMethod, object[] __args)
        {
            try { BtcLiveSym.MirrorCall(__originalMethod, __args); }
            catch (Exception e) { BtcLog.Error("Hook", "LiveSym nature: " + e); }
        }
    }
}
