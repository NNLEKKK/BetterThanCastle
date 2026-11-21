using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.External.Factories;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lua;
using Brix.UI.Icons;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Altar recipe of the wormhole ward (BtcWormWard): 1 iron ingot + 30 brimstone, work 10 (the sentinel ward recipe with other
    /// ingredients), it makes a sentinel ward object that the host marks as a wormhole ward when the recipe finishes (Recipe.Finish).
    /// The RecipeInfo is registered once at plugin start (same id on every mod peer). Lua altar panel: LuaCrafting knows it as the extra
    /// enum value <see cref="LuaValue"/> (dictionaries extended after LuaCrafting.Register), and the loaded Meta_Recipe.lua /
    /// Meta_Operable.lua get a few lines that add it to the altar recipes when the rule is on (Patches/WormWardCraftPatches.cs).
    /// Pie menu: the altar wheel gets "Queue Wormhole Ward" (Livre text, BtcLivre).
    /// </summary>
    internal static class BtcWormWardCraft
    {
        internal const int LuaValue = 200;     // LuaCrafting.LuaRecipe has no such member: the wormhole ward recipe
        internal static RecipeInfo WormRecipe;
        private static bool _mapped;

        internal static void Init()
        {
            if (WormRecipe != null) return;
            try
            {
                WormRecipe = new RecipeMaker
                {
                    Work = 10,
                    Heat = 0,
                    Creates = ObjetsDynamiques.SentinelWard,
                    QteCreated = 1,
                    DropOnCompletion = false,
                    Icon = BtcWormWardLook.Key,
                    Drawing = BtcWormWardLook.Key
                }.With(Adjectif.iron, 1).With(Adjectif.orangeCrystal, 30).Make();
                BtcLog.Info("WormWard", "recipe id=" + WormRecipe.id + " (sentinel id=" + CookBook.SentinelWard.id + ")");
            }
            catch (Exception e) { BtcLog.Error("WormWard", "recipe: " + e); }
        }

        internal static bool IsWorm(RecipeInfo r) { return r != null && ReferenceEquals(r, WormRecipe); }

        /// <summary>LuaCrafting.Register postfix: the extra enum value maps to our recipe both ways.</summary>
        internal static void MapLua()
        {
            if (_mapped || WormRecipe == null) return;
            try
            {
                var toInfo = AccessTools.Field(typeof(LuaCrafting), "_luaRecipeToRecipeInfo").GetValue(null) as Dictionary<LuaCrafting.LuaRecipe, List<RecipeInfo>>;
                var toLua = AccessTools.Field(typeof(LuaCrafting), "_recipeInfoToLuaRecipe").GetValue(null) as Dictionary<RecipeInfo, LuaCrafting.LuaRecipe>;
                if (toInfo == null || toLua == null) { BtcLog.Error("WormWard", "LuaCrafting maps not found"); return; }
                LuaCrafting.LuaRecipe v = (LuaCrafting.LuaRecipe)LuaValue;
                toInfo[v] = new List<RecipeInfo> { WormRecipe };
                toLua[WormRecipe] = v;
                _mapped = true;
            }
            catch (Exception e) { BtcLog.Error("WormWard", "MapLua: " + e); }
        }

        /// <summary>Lua global __BtcWormRecipe(): the enum value of the recipe while the rule is on, else nil.</summary>
        private static DynValue LuaRecipe(ScriptExecutionContext sec, CallbackArguments args)
        {
            try
            {
                if (!BtcWormWard.RuleOn || WormRecipe == null) return DynValue.Nil;
                MapLua();
                return DynValue.FromObject<LuaCrafting.LuaRecipe>(sec.OwnerScript, (LuaCrafting.LuaRecipe)LuaValue, false);   // the same proxy vanilla hands out (CraftingStation GetQueue)
            }
            catch (Exception e) { BtcLog.Error("WormWard", "__BtcWormRecipe: " + e); return DynValue.Nil; }
        }

        internal static readonly DynValue LuaRecipeCb = DynValue.NewCallback(LuaRecipe, "__BtcWormRecipe");

        /// <summary>Lua global __BtcWormIcon(): the white wormhole ward icon (sprite) or nil.</summary>
        private static DynValue LuaIcon(ScriptExecutionContext sec, CallbackArguments args)
        {
            Sprite s = BtcWormWardLook.Icon64();
            return s != null ? DynValue.FromObject(sec.OwnerScript, s) : DynValue.Nil;
        }

        internal static readonly DynValue LuaIconCb = DynValue.NewCallback(LuaIcon, "__BtcWormIcon");

        private static string Q(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

        /// <summary>BrixScriptLoader.LoadFile postfix: extra Lua for the altar panel files (game files stay untouched).</summary>
        internal static string PatchLua(string file, string code, Table globals)
        {
            if (code == null || file == null) return code;
            string f = file.Replace('\\', '/');
            bool rec = f.EndsWith("LUI/Meta/Meta_Recipe.lua"), op = f.EndsWith("LUI/Meta/Meta_Operable.lua");
            if (f.EndsWith("LUI/Menus/GameMenu.lua", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (globals != null) { BtcWormWardBuild.SetGlobals(globals); globals.Set("__BtcWormIcon", LuaIconCb); }
                    return BtcWormWardBuild.PatchGameMenu(code);
                }
                catch (Exception e) { BtcLog.Error("WormWard", "PatchLua GameMenu: " + e); return code; }
            }
            if (!rec && !op) return code;
            try
            {
                if (globals != null) globals.Set("__BtcWormRecipe", LuaRecipeCb);
                if (globals != null) globals.Set("__BtcWormIcon", LuaIconCb);
                int ret = code.LastIndexOf("return _t", StringComparison.Ordinal);
                if (ret < 0) { BtcLog.Error("WormWard", "no 'return _t' in " + f); return code; }
                string add;
                if (rec)
                {
                    bool ru = BtcLang.Russian;
                    string name = ru ? "Вард-червоточина" : "Wormhole Ward";
                    string info = ru ? "Белый вард, который не стреляет. Свои варды-червоточины и кристаллы связаны для фантомов: фантом мгновенно переходит между ними. Не работает, если в нём или над ним есть блок."
                                     : "A white ward that never shoots. Own wormhole wards and crystals are linked for phantoms: a phantom steps instantly from one to another. Does not work with a block in it or right above it.";
                    add = "\ndo local r = __BtcWormRecipe and __BtcWormRecipe() if r ~= nil then _t:Add(r, {Name = function() return " + Q(name)
                        + " end, Info = function() return " + Q(info) + " end, Icon = function() return (__BtcWormIcon and __BtcWormIcon()) or IconKeys._Sentinel:Get64() end}) end end\n";
                }
                else
                {
                    add = "\ndo local r = __BtcWormRecipe and __BtcWormRecipe() if r ~= nil and fy_Altar ~= nil and _t[fy_Altar] ~= nil then"
                        + " local l = _t[fy_Altar].Recipes local at = #l + 1 for i = 1, #l do if l[i] == Recipe.SentinelWard then at = i + 1 end end"
                        + " table.insert(l, at, r) end end\n";
                }
                BtcLog.Info("WormWard", "lua patched " + (rec ? "Meta_Recipe" : "Meta_Operable") + " (rule " + (BtcWormWard.RuleOn ? "on" : "off") + ")");
                return code.Substring(0, ret) + add + code.Substring(ret);
            }
            catch (Exception e) { BtcLog.Error("WormWard", "PatchLua: " + e); return code; }
        }

        /// <summary>Recipe.Finish prefix: the produced sentinel ward of our recipe becomes a wormhole ward.</summary>
        internal static Action<GameObject> Wrap(Action<GameObject> post)
        {
            return go =>
            {
                if (post != null) post(go);
                try { BtcWormWard.Mark(go, false); }
                catch (Exception e) { BtcLog.Error("WormWard", "mark crafted: " + e); }
            };
        }

        // ------------------------------------------------------------------ pie menu (altar wheel)
        private static MethodInfo _order;

        internal static void Install()
        {
            Brix.Legacy.Instructions.conditions["Condition_Worm_Ward_Craft"] = () => BtcWormWard.RuleOn;
            Brix.Legacy.Instructions.commendes["Event_Workbench_OrderWormWard"] = () => Order();
        }

        private static void Order()
        {
            try
            {
                if (!BtcWormWard.RuleOn || WormRecipe == null) return;
                if (_order == null) _order = AccessTools.Method(typeof(Brix.Legacy.Instructions), "OrderRecipe");
                _order.Invoke(null, new object[] { 1, new RecipeInfo[] { WormRecipe } });
            }
            catch (Exception e) { BtcLog.Error("WormWard", "order: " + e); }
        }
    }
}
