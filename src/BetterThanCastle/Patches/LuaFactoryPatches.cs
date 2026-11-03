using System;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Lua;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Shared editor panel (BtcEditorShare): on a client, sidebar spawns of networked objects are forwarded to the host
    /// (msg 67). Only when BC ON + host option gen.editor + multiplayer client; otherwise vanilla runs untouched.
    /// </summary>
    [HarmonyPatch(typeof(LuaFactory), "InstantiateFromCLR")]
    internal static class LuaFactoryInstantiateFromCLRPatch
    {
        private static bool Prefix(Factory.AssetKey key, Faction faction, Vector3 position, Quaternion rotation, ref LuaDriver __result)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try
            {
                LuaDriver d;
                if (!BtcEditorShare.TryForward(key, faction, position, rotation, out d)) return true;
                __result = d;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "LuaFactory.InstantiateFromCLR: " + e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Toolbag), "lua_EquipTool")]
    internal static class ToolbagLuaEquipToolPatch
    {
        private static bool Prefix(CallbackArguments args, ref DynValue __result)
        {
            if (!BtcNetworkGate.Enabled) return true;
            try
            {
                if (!BtcEditorShare.TryRecordTool(args)) return true;
                __result = DynValue.Void;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "Toolbag.lua_EquipTool: " + e);
                return true;
            }
        }
    }
}
