using System;
using System.Collections.Generic;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Network;
using Assets.Standard_Assets.Brix.Game.Components;
using Brix.Game.Semantique;
using Smooth.Algebraics;
using Brix.Common.Collections;
using Brix.Utils;
using Smooth.Slinq;
using Smooth.Slinq.Context;
using UnityEngine;

namespace BetterThanCastle
{
    /// Crafting station sections of the popup (GameMenu.lua 3026-3322 over Data_Operable.lua, Data_Recipe.lua, Meta_Operable/Recipe/Resource.lua):
    /// craft slots, required resources of the active recipe, finished goods in the station, the queue with loop / clear.
    /// The Lua Meta tables are mirrored here as static data; recipes and resources come from CookBook / Adjectif directly (no LuaCrafting maps needed).
    /// The wormhole ward recipe (BtcWormWardCraft, the Lua Meta patch's twin) is added to the altar while the phantom ward rule is on.
    internal static partial class BtcGhPopup
    {
        private static CraftingStation _op;
        private static int _opKind = -1;
        private static RSlot[] _opSlots = new RSlot[0];
        private static int[] _content;

        private sealed class RSlot { internal string Icon; internal bool Worm; internal RecipeInfo[] Infos; }

        private sealed class RData { internal int[] InIdx = new int[0]; internal int[] InNeed = new int[0]; internal int OutIdx = -1; }

        // Meta_Resource.lua order (index 0 = None); icons are the Meta icons
        private static readonly string[] MetaIcon =
        {
            "_Stockpile_Empty", "_Log", "_Brick", "_Plank", "_Raw_Orange_Crystal", "_Raw_Stone", "_Iron", "_Raw_Blue_Crystal", "_Plant", "_Rope",
            "_Fabric", "_Raw_Iron", "_Glass_Ingot", "_Cog", "_Bomb", "_Explosive_Barrel", "_Sentinel", "_Healing_Ward", "_Shield_Ward", "_Lantern", "_Pylon",
        };

        private static Dictionary<AdjectiveKey, int> _meta;
        private static readonly Dictionary<RecipeInfo, RData> DataCache = new Dictionary<RecipeInfo, RData>();
        private static Dictionary<RecipeInfo, string> _recipeIcons;
        private static int[] _outOrder;

        private static void EnsureMeta()
        {
            if (_meta != null) return;
            Dictionary<AdjectiveKey, int> m = new Dictionary<AdjectiveKey, int>();
            Meta(m, Adjectif.woodBlock.GetKey(), 1); Meta(m, Adjectif.stoneBlock.GetKey(), 2); Meta(m, Adjectif.plankBlock.GetKey(), 3);
            Meta(m, Adjectif.orangeCrystal.GetKey(), 4); Meta(m, Adjectif.stones.GetKey(), 5); Meta(m, Adjectif.iron.GetKey(), 6);
            Meta(m, Adjectif.blueCrystal.GetKey(), 7); Meta(m, Adjectif.plant.GetKey(), 8); Meta(m, Adjectif.rope.GetKey(), 9);
            Meta(m, Adjectif.fabric.GetKey(), 10); Meta(m, Adjectif.rawIron.GetKey(), 11); Meta(m, Adjectif.glass.GetKey(), 12);
            Meta(m, Adjectif.cog.GetKey(), 13); Meta(m, Adjectif.bomb.GetKey(), 14); Meta(m, Adjectif.explosiveBarrel.GetKey(), 15);
            Meta(m, Adjectif.sentinelWardAdj.GetKey(), 16); Meta(m, Adjectif.healingWardAdj.GetKey(), 17); Meta(m, Adjectif.captureWardAdj.GetKey(), 18);
            Meta(m, Adjectif.lanternWardAdj.GetKey(), 19); Meta(m, Adjectif.pylonWardAdj.GetKey(), 20);
            _content = new int[MetaIcon.Length];
            _meta = m;
        }

        private static void Meta(Dictionary<AdjectiveKey, int> m, AdjectiveKey k, int i) { if (k != null && !m.ContainsKey(k)) m.Add(k, i); }

        /// Data.Recipe info of one queued recipe: required resources in Meta.Resource order (heat counts as brimstone) and its output resource.
        private static RData DataOf(RecipeInfo r)
        {
            RData d;
            if (r == null) return null;
            if (DataCache.TryGetValue(r, out d)) return d;
            d = new RData();
            try
            {
                EnsureMeta();
                int[] need = new int[MetaIcon.Length];
                foreach (Tuple<AdjectiveKey, int> t in r.Ingredients())
                {
                    int idx;
                    if (_meta.TryGetValue(t._1, out idx)) need[idx] += t._2;
                }
                int heat;
                if (r.heat > 0 && _meta.TryGetValue(Adjectif.orangeCrystal.GetKey(), out heat)) need[heat] += r.heat;
                List<int> ii = new List<int>(), nn = new List<int>();
                for (int i = 1; i < need.Length; i++) if (need[i] > 0) { ii.Add(i); nn.Add(need[i]); }
                d.InIdx = ii.ToArray(); d.InNeed = nn.ToArray();
                Ressource res = Apparence.PrincipaleOf(Factory.Peek(r.Creates));
                int o;
                if (res != null && _meta.TryGetValue(res.GetKey(), out o)) d.OutIdx = o;
            }
            catch (Exception e) { BtcLog.Warn("GameHud", "recipe data: " + e.Message); }
            DataCache[r] = d;
            return d;
        }

        // Meta_Recipe.lua icons by recipe
        private static void EnsureRecipeIcons()
        {
            if (_recipeIcons != null) return;
            Dictionary<RecipeInfo, string> m = new Dictionary<RecipeInfo, string>();
            m[CookBook.Planks] = "_Plank"; m[CookBook.IronIngot] = "_Iron"; m[CookBook.GlassIngot] = "_Glass_Ingot"; m[CookBook.FabricPile] = "_Fabric";
            m[CookBook.RopeBallot] = "_Rope"; m[CookBook.Cog] = "_Cog"; m[CookBook.Bomb] = "_Bomb"; m[CookBook.BearTrap] = "_Beartrap";
            m[CookBook.ExplosiveBarrel] = "_Explosive_Barrel"; m[CookBook.SentinelWard] = "_Sentinel"; m[CookBook.HealingWard] = "_Healing_Ward";
            m[CookBook.CaptureWard] = "_Capture_Stone"; m[CookBook.LanternWard] = "_Lantern"; m[CookBook.PylonWard] = "_Pylon";
            m[CookBook.Poleaxe] = "_Poleaxe"; m[CookBook.KettleHat] = "_Kettle_Hat"; m[CookBook.Sword] = "_Sword"; m[CookBook.Shield] = "_Shield";
            m[CookBook.Helmet] = "_Helmet"; m[CookBook.Bow] = "_Bow"; m[CookBook.Quiver] = "_Quiver"; m[CookBook.Cap] = "_Cap";
            m[CookBook.CrossbowWeapon] = "_Crossbow"; m[CookBook.CrossbowPouch] = "_Pouch"; m[CookBook.CrossbowMorion] = "_Kepi";
            m[CookBook.Staff] = "_Staff"; m[CookBook.Sachel] = "_Satchel"; m[CookBook.ArtificerHat] = "_Artificer_Hat";
            m[CookBook.AlchemistPouch] = "_Backpack"; m[CookBook.AlchemistHat] = "_Bonnet";
            _recipeIcons = m;
        }

        private static Sprite RecipeSprite(RecipeInfo r)
        {
            if (r == null) return Ico("Cancel");
            if (BtcWormWardCraft.IsWorm(r)) return BtcWormWardLook.Icon64() ?? Ico("_Sentinel");
            EnsureRecipeIcons();
            string n;
            return Ico(_recipeIcons.TryGetValue(r, out n) ? n : "Cancel");
        }

        private static RSlot Slot(string icon, params RecipeInfo[] infos) { return new RSlot { Icon = icon, Infos = infos }; }

        /// Meta_Operable.lua Recipes of a station kind.
        private static RSlot[] SlotsOf(int kind)
        {
            switch (kind)
            {
                case 0: return new[] { Slot("_Iron", CookBook.IronIngot), Slot("_Glass_Ingot", CookBook.GlassIngot) }; // Furnace
                case 1: return new[] { Slot("_Plank", CookBook.Planks) }; // ChoppingBlock
                case 2: return new[] { Slot("_Fabric", CookBook.FabricPile), Slot("_Rope", CookBook.RopeBallot) }; // Loom
                case 3: return new[] { Slot("_Halberdier_Kit", CookBook.Poleaxe, CookBook.KettleHat), Slot("_Archer_Kit", CookBook.Bow, CookBook.Quiver, CookBook.Cap), Slot("_Lantern", CookBook.LanternWard) }; // Workbench
                case 4: return new[] { Slot("_Knight_Kit", CookBook.Sword, CookBook.Shield, CookBook.Helmet), Slot("_Cog", CookBook.Cog) }; // Forge (fy_Anvil)
                case 5: return new[] { Slot("_Arbalist_Kit", CookBook.CrossbowWeapon, CookBook.CrossbowPouch, CookBook.CrossbowMorion), Slot("_Beartrap", CookBook.BearTrap) }; // MachineShop
                case 6: return new[] { Slot("_Alchemist_Kit", CookBook.AlchemistPouch, CookBook.AlchemistHat), Slot("_Bomb", CookBook.Bomb), Slot("_Explosive_Barrel", CookBook.ExplosiveBarrel) }; // Lab
                case 7:
                    {
                        List<RSlot> a = new List<RSlot>();
                        a.Add(Slot("_Artificer_Kit", CookBook.Staff, CookBook.Sachel, CookBook.ArtificerHat));
                        a.Add(Slot("_Sentinel", CookBook.SentinelWard));
                        if (BtcWormWard.RuleOn && BtcWormWardCraft.WormRecipe != null) a.Add(new RSlot { Worm = true, Infos = new[] { BtcWormWardCraft.WormRecipe } });
                        a.Add(Slot("_Healing_Ward", CookBook.HealingWard));
                        a.Add(Slot("_Capture_Stone", CookBook.CaptureWard));
                        a.Add(Slot("_Pylon", CookBook.PylonWard));
                        return a.ToArray();
                    }
            }
            return new RSlot[0];
        }

        private static int KindOf(CraftingStation s)
        {
            if (s is Furnace) return 0;
            if (s is ChoppingBlock) return 1;
            if (s is Loom) return 2;
            if (s is Workbench) return 3;
            if (s is Forge) return 4;
            if (s is MachineShop) return 5;
            if (s is Lab) return 6;
            if (s is Altar) return 7;
            return -1;
        }

        private static CraftingStation SelectedStation()
        {
            CraftingStation s = UIGameObserver.crafting != null ? UIGameObserver.crafting.CurrentSelected : null;
            return s == null || s.IsNullOrReleased() ? null : s;
        }

        /// Data.Operable:OnSetSelected
        private static void UpdateOp()
        {
            _op = SelectedStation();
            _opKind = _op != null ? KindOf(_op) : -1;
            _opSlots = _op != null ? SlotsOf(_opKind) : new RSlot[0];
            ReadOpContent();
        }

        /// CraftingStation.lua_GetContent: resource counts in the station inventory (Meta resources only).
        private static void ReadOpContent()
        {
            EnsureMeta();
            Array.Clear(_content, 0, _content.Length);
            if (_op == null) return;
            try
            {
                foreach (Ressource r in _op.recepteur.ContentDescription.GetRessources().NoLeak<Ressource, PredicateContext<Ressource, SelectContext<Ressource, KeyValuePair<Type, Adjectif>, DictionaryContext<Type, Adjectif>>>>())
                {
                    int idx;
                    if (r.valeur != 0 && _meta.TryGetValue(r.GetKey(), out idx)) _content[idx] = r.valeur;
                }
            }
            catch (Exception e) { BtcLog.Warn("GameHud", "station content: " + e.Message); }
        }

        private static bool OpHas() { return _op != null; }
        private static bool HasQueue() { return _op != null && _op.QueuedRecipes != null && _op.QueuedRecipes.Count != 0; }
        private static RData ActiveData() { return HasQueue() ? DataOf(_op.QueuedRecipes[0]) : null; }

        /// Data.Operable:HasOutputContent: any finished good of this station's recipes sits in the inventory.
        private static bool HasOutputContent()
        {
            if (_op == null) return false;
            for (int i = 0; i < _opSlots.Length; i++)
                for (int j = 0; j < _opSlots[i].Infos.Length; j++)
                {
                    RData d = DataOf(_opSlots[i].Infos[j]);
                    if (d != null && d.OutIdx >= 0 && _content[d.OutIdx] != 0) return true;
                }
            return false;
        }

        /// Data.Recipe:GetOutputResources: output resources over all recipes in Meta.Recipe order (kit pieces have none).
        private static int[] OutOrder()
        {
            if (_outOrder != null) return _outOrder;
            RecipeInfo[] order =
            {
                CookBook.Planks, CookBook.IronIngot, CookBook.GlassIngot, CookBook.FabricPile, CookBook.RopeBallot, CookBook.Cog, CookBook.Bomb,
                CookBook.BearTrap, CookBook.ExplosiveBarrel, CookBook.SentinelWard, CookBook.HealingWard, CookBook.CaptureWard, CookBook.LanternWard, CookBook.PylonWard,
            };
            List<int> o = new List<int>();
            for (int i = 0; i < order.Length; i++)
            {
                RData d = DataOf(order[i]);
                if (d != null && d.OutIdx >= 0 && !o.Contains(d.OutIdx)) o.Add(d.OutIdx);
            }
            _outOrder = o.ToArray();
            return _outOrder;
        }

        // ------------------------------------------------------------------ sections
        private static void DefineOperableItems()
        {
            Items.Add(RecipesSec());
            Items.Add(new Div(M_OPSEL | M_OPQ | M_OPCONTENT, HasQueue, 8f)); Items.Add(NeedSec());
            Items.Add(new Div(M_OPSEL | M_OPQ | M_OPCONTENT, HasOutputContent, 8f)); Items.Add(FinishedSec());
            Items.Add(new Div(M_OPSEL | M_OPQ | M_OPLOOP, HasQueue, 8f)); Items.Add(QueueSec());
        }

        private static Sec RecipesSec()
        {
            return new Sec(M_OPSEL | M_OPQ, OpHas, "##operable_craft", delegate(Sec s)
            {
                for (int i = 0; i < 6; i++)
                {
                    int slot = i;
                    s.Add(new Btn
                    {
                        Visible = delegate { return _op != null && slot < _opSlots.Length; },
                        IconFn = delegate { return slot < _opSlots.Length ? SlotSprite(_opSlots[slot]) : null; },
                        IconColor = Color.white, IconSize = 64f,
                        HasHotkey = true, Hotkey = Key("Projects", "project_QueueSlot" + (slot + 1)),
                        OnAction = delegate { ActQueueSlot(slot + 1); },
                    });
                }
            });
        }

        private static Sprite SlotSprite(RSlot r)
        {
            if (r.Worm) return BtcWormWardLook.Icon64() ?? Ico("_Sentinel");
            return Ico(r.Icon);
        }

        private static Sec NeedSec()
        {
            return new Sec(M_OPSEL | M_OPQ | M_OPCONTENT, HasQueue, "##operable_resources_requirements", delegate(Sec s)
            {
                for (int i = 0; i < 8; i++)
                {
                    int k = i;
                    int lh = -1, ln = -1;
                    string ls = "";
                    s.Add(new Btn
                    {
                        Visible = delegate { RData d = ActiveData(); return d != null && k < d.InIdx.Length; },
                        IconFn = delegate { RData d = ActiveData(); return d != null && k < d.InIdx.Length ? Ico(MetaIcon[d.InIdx[k]]) : null; },
                        IconColor = Color.white, IconSize = 64f, CountColor = BtcGhKit.Red,
                        CountFn = delegate
                        {
                            RData d = ActiveData();
                            if (d == null || k >= d.InIdx.Length) return ls;
                            int have = _content[d.InIdx[k]], need = d.InNeed[k];
                            if (have != lh || need != ln) { lh = have; ln = need; ls = Num(have) + "/" + Num(need); }
                            return ls;
                        },
                    });
                }
            });
        }

        private static Sec FinishedSec()
        {
            return new Sec(M_OPSEL | M_OPQ | M_OPCONTENT, HasOutputContent, "##operable_resources_finished", delegate(Sec s)
            {
                int[] order = OutOrder();
                for (int i = 0; i < order.Length; i++)
                {
                    int idx = order[i];
                    s.Add(new Btn
                    {
                        Visible = delegate { return _op != null && _content[idx] != 0; },
                        IconName = MetaIcon[idx], IconColor = Color.white, IconSize = 64f, CountColor = BtcGhKit.Red,
                        CountFn = delegate { return Num(_content[idx]); },
                    });
                }
            });
        }

        private static Sec QueueSec()
        {
            return new Sec(M_OPSEL | M_OPQ | M_OPLOOP, HasQueue, "##operable_queue", delegate(Sec s)
            {
                for (int i = 0; i < 8; i++) // Data.Operable:GetQueueSize() is the constant 8 (BtcCraftQueue's longer tail has no slot, as in the Lua)
                {
                    int k = i;
                    s.Add(new Btn
                    {
                        Visible = delegate { return HasQueue() && _op.QueuedRecipes.Count > k; },
                        IconFn = delegate { return HasQueue() && _op.QueuedRecipes.Count > k ? RecipeSprite(_op.QueuedRecipes[k]) : null; },
                        IconColor = Color.white, IconSize = 64f,
                        OnAction = delegate { ActRemoveFromQueue(k); },
                        Highlight = k == 0 ? (Func<int>)delegate { return 15; } : null,
                    });
                }
                s.Add(new InnerDiv(HasQueue));
                s.Add(new Btn { Visible = HasQueue, IconName = "_UI_Wheel", IconSize = 32f, IconColorFn = LoopColor, HasHotkey = true, Hotkey = Key("Projects", "project_ToggleLoop"), OnAction = ActLoop });
                s.Add(new Btn { Visible = HasQueue, IconName = "_UI_X", IconSize = 32f, IconColor = BtcGhKit.Red, HasHotkey = true, Hotkey = Key("Projects", "project_ClearQueue"), OnAction = ActClear });
            });
        }

        private static Color LoopColor() { return _op != null && _op.Looping ? BtcGhKit.Red : BtcGhKit.Gray; }

        // ------------------------------------------------------------------ actions (CraftingStation.lua_AddToQueue / RemoveFromQueue / ClearQueue / SetIsQueueLooping)
        private static UNetProjectCmd StationCmd() { return User.LocalUser.GetComponent<UNetProjectCmd>(); }

        /// _m.bh.operableRecipes[i].OnAction: only while that slot is visible.
        internal static void ActQueueSlot(int slot)
        {
            try
            {
                CraftingStation s = SelectedStation();
                if (s == null || _op == null || slot < 1 || slot > _opSlots.Length) return;
                RecipeInfo[] infos = _opSlots[slot - 1].Infos;
                for (int i = 0; i < infos.Length; i++) StationCmd().CallCmdcraftingQueueRecipe(s.gameObject, infos[i].id, 1);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "queue slot: " + e.Message); }
        }

        private static void ActRemoveFromQueue(int zeroBased)
        {
            CraftingStation s = SelectedStation();
            if (s != null) StationCmd().CallCmdcraftingRemoveAt(s.gameObject, zeroBased);
        }

        internal static void ActClear()
        {
            CraftingStation s = SelectedStation();
            if (s != null) StationCmd().CallCmdcraftingDoNothing(s.gameObject);
        }

        internal static void ActLoop()
        {
            CraftingStation s = SelectedStation();
            if (s != null) StationCmd().CallCmdcraftingLoop(s.gameObject, !s.Looping);
        }
    }
}
