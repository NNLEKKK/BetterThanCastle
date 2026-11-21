using System;
using System.Reflection;
using Brix.Game.Components;
using Brix.Lua;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>Wormhole ward recipe (BtcWormWardCraft): LuaCrafting learns the extra enum value once its maps exist.</summary>
    [HarmonyPatch(typeof(LuaCrafting), "Register")]
    internal static class WormWardLuaCraftingPatch
    {
        private static void Postfix(Script script)
        {
            try
            {
                BtcWormWardCraft.Init();
                BtcWormWardCraft.MapLua();
                if (script != null) script.Globals.Set("__BtcWormRecipe", BtcWormWardCraft.LuaRecipeCb);
                if (script != null) script.Globals.Set("__BtcWormIcon", BtcWormWardCraft.LuaIconCb);
                if (script != null) BtcWormWardBuild.SetGlobals(script.Globals);
#if DEV
                if (script != null) Dev.BtcDevWormWard.Scripts.Add(new WeakReference(script));
#endif
            }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard LuaCrafting.Register: " + e.Message); }
        }
    }

    /// <summary>Meta_Recipe.lua / Meta_Operable.lua as the script loader hands them out: the altar lists the wormhole ward (rule on).</summary>
    [HarmonyPatch]
    internal static class WormWardLoadFilePatch
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo m in typeof(BrixScriptLoader).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name.EndsWith("LoadFile", StringComparison.Ordinal)) return m;
            return null;
        }

        private static void Postfix(string file, Table globalContext, ref object __result)
        {
            try
            {
                string s = __result as string;
                if (s == null || file == null) return;
                __result = BtcWormWardCraft.PatchLua(file, s, globalContext);
            }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard LoadFile: " + e.Message); }
        }
    }

    /// <summary>The produced object of the wormhole ward recipe gets its mark (host: Recipe.Finish runs only on the server).</summary>
    [HarmonyPatch(typeof(Recipe), "Finish")]
    internal static class WormWardRecipeFinishPatch
    {
        private static void Prefix(Recipe __instance, ref Action<GameObject> postProcess)
        {
            try { if (BtcWormWardCraft.IsWorm(__instance.Info)) postProcess = BtcWormWardCraft.Wrap(postProcess); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard Recipe.Finish: " + e.Message); }
        }
    }
}

namespace BetterThanCastle.Patches
{
    /// <summary>Wormhole ward icon (BtcWormWardLook.Key, never registered in IconKey's tables): IconDB answers it with the white icon,
    /// the sentinel icon until the white one can be made.</summary>
    [HarmonyPatch(typeof(Brix.UI.Icons.IconDB), "GetIcon", new[] { typeof(Brix.UI.Icons.IconKey) })]
    internal static class WormWardIconGetPatch
    {
        private static bool Prefix(Brix.UI.Icons.IconKey key, ref Brix.UI.Icons.MetaIcon __result)
        {
            if (key == null || key.Id != BtcWormWardLook.IconId) return true;
            try
            {
                __result = BtcWormWardLook.EnsureIcon();
                if (__result != null) return false;
            }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard IconDB.GetIcon: " + e.Message); }
            __result = Brix.UI.Icons.IconDB._Unsafe_GetIcon_NEVERUSE(null, "_Sentinel");
            return false;
        }
    }

    /// <summary>Lookup by name (pie menu "Icon : _BtcWormWard").</summary>
    [HarmonyPatch(typeof(Brix.UI.Icons.IconDB), "_Unsafe_GetIcon_NEVERUSE")]
    internal static class WormWardIconNamePatch
    {
        private static bool Prefix(string name, ref Brix.UI.Icons.MetaIcon __result)
        {
            if (name != BtcWormWardLook.IconName) return true;
            try { __result = BtcWormWardLook.EnsureIcon(); }
            catch (Exception e) { BtcLog.Error("Hook", "WormWard IconDB name: " + e.Message); }
            if (__result == null) return true;   // not ready: vanilla lookup (finds nothing, the pie keeps no icon) - the sentinel below
            return false;
        }

        private static void Postfix(string name, ref Brix.UI.Icons.MetaIcon __result)
        {
            if (__result == null && name == BtcWormWardLook.IconName) __result = Brix.UI.Icons.IconDB._Unsafe_GetIcon_NEVERUSE(null, "_Sentinel");
        }
    }

    /// <summary>Clients resolve the icon id of synced icon lists (NetworkIcons) back to the key.</summary>
    [HarmonyPatch(typeof(Brix.UI.Icons.IconKey), "FromId")]
    internal static class WormWardIconFromIdPatch
    {
        private static void Postfix(int id, ref Brix.UI.Icons.IconKey __result)
        {
            if (id == BtcWormWardLook.IconId) __result = BtcWormWardLook.Key;
        }
    }
}
