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
using UnityEngine.UI;

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

        internal static Sprite RecipeIcon(RecipeInfo r) { return RecipeSprite(r); }

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
            _bp = _op == null ? BtcBpPreset.SelectedStation : null;
            _opKind = _op != null ? KindOf(_op) : (_bp != null ? BtcBpPreset.StationKind(_bp) : -1);
            _opSlots = OpOn() ? SlotsOf(_opKind) : new RSlot[0];
            _opParts = PartsOf(_opSlots);
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

        private static bool OpHas() { return OpOn(); }
        private static bool HasQueue() { List<RecipeInfo> q = OpQueue(); return q != null && q.Count != 0; }
        private static bool HasNeed() { return _bp == null && HasQueue(); }
        private static RData ActiveData() { return HasNeed() ? DataOf(_op.QueuedRecipes[0]) : null; }

        // ------------------------------------------------------------------ station mark (BtcBpPreset): the same panel edits the preset of the selected mark
        private static Blueprint _bp;

        /// preset changed (sel: the selected mark changed)
        internal static void OnBpChanged(bool sel)
        {
            try { if (sel) UpdateOp(); Refresh(M_OPSEL | M_OPQ | M_OPLOOP | M_OPCONTENT); }
            catch (Exception e) { BtcLog.Error("GameHud", "station mark: " + e.Message); }
        }

        private static bool OpOn() { return _op != null || _bp != null; }
        private static List<RecipeInfo> OpQueue() { return _bp != null ? BtcBpPreset.Queue(_bp) : (_op != null ? _op.QueuedRecipes : null); }
        private static bool OpLoop() { return _bp != null ? BtcBpPreset.Loop(_bp) : _op != null && _op.Looping; }
        private static bool OpLimit() { return _bp != null ? BtcBpPreset.LimitOn(_bp) : BtcWorkbench.LimitOn(_op); }
        private static bool OpHelp() { return _bp != null ? BtcBpPreset.HelpOn(_bp) : BtcWorkbench.HelpOn(_op); }

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
            Items.Add(new Div(M_OPSEL | M_OPQ | M_OPCONTENT, HasNeed, 8f)); Items.Add(NeedSec());
            Items.Add(new Div(M_OPSEL | M_OPQ | M_OPCONTENT, HasOutputContent, 8f)); Items.Add(FinishedSec());
            Items.Add(new Div(M_OPSEL | M_OPQ | M_OPLOOP, HasQueue, 8f)); Items.Add(QueueSec());        }

        private static Sec RecipesSec()
        {
            return new Sec(M_OPSEL | M_OPQ, OpHas, "##operable_craft", delegate(Sec s)
            {
                for (int i = 0; i < 6; i++)
                {
                    int slot = i;
                    s.Add(new Btn
                    {
                        Visible = delegate { return OpOn() && slot < _opSlots.Length; },
                        IconFn = delegate { return slot < _opSlots.Length ? SlotSprite(_opSlots[slot]) : null; },
                        IconColor = Color.white, IconSize = 64f,
                        HasHotkey = true, Hotkey = Key("Projects", "project_QueueSlot" + (slot + 1)),
                        OnAction = delegate { ActQueueSlot(slot + 1); },
                    });
                }
                // Better Workbenches: one button per kit part (spear only, hat only...)
                s.Add(new InnerDiv(HasParts));
                for (int i = 0; i < 6; i++)
                {
                    int p = i;
                    s.Add(new Btn
                    {
                        Visible = delegate { return HasParts() && p < _opParts.Length; },
                        IconFn = delegate { return p < _opParts.Length ? RecipeSprite(_opParts[p]) : null; },
                        IconColor = Color.white, IconSize = 48f,
                        Tip = delegate { return BtcLang.Tr("Queue only this kit part"); },
                        HasHotkey = true, Hotkey = BindText("craft_part_" + (p + 1)),
                        OnAction = delegate { ActQueuePart(p); },
                    });
                }
                // Better Workbenches: rack limit mode of this station (red = on: kit parts without a free rack slot are skipped)
                s.Add(new Btn
                {
                    Visible = HasParts,
                    IconName = "_Tool_Rack", IconSize = 40f,
                    IconColorFn = delegate { return OpLimit() ? BtcGhKit.Red : BtcGhKit.Gray; },
                    Tip = delegate
                    {
                        return BtcLang.Tr(OpLimit() ? "Rack limit ON: kit parts no rack has room for are skipped, the next order is crafted"
                            : "Rack limit OFF: kit parts are crafted without limit") + " (" + BtcLang.Tr("click to switch for this station") + ")";
                    },
                    HasHotkey = true, Hotkey = BindText("craft_rack_toggle"),
                    OnAction = ActRackToggle,
                });
                // Better Workbenches: helper mode of this station (red = on: one more worker brings what the next orders need), every station
                s.Add(new InnerDiv(delegate { return HelpVisible() && !HasParts(); }));
                s.Add(new Btn
                {
                    Visible = HelpVisible,
                    IconName = "_Bricktron", IconSize = 40f,
                    IconColorFn = delegate { return OpHelp() ? BtcGhKit.Red : BtcGhKit.Gray; },
                    Tip = delegate
                    {
                        return BtcLang.Tr(OpHelp() ? "Helper ON: while one worker crafts, one more brings what the next orders need"
                            : "Helper OFF: only the crafting worker fetches, for the current order") + " (" + BtcLang.Tr("click to switch for this station") + ")";
                    },
                    HasHotkey = true, Hotkey = BindText("craft_help_toggle"),
                    OnAction = ActHelpToggle,
                });
            });
        }

        private static bool HelpVisible() { return BtcWorkbench.On && (_bp != null ? BtcBpPreset.HelperKind(_bp) : _op != null && BtcWorkbench.HelperStation(_op)); }

        private static void ActRackToggle()
        {
            try { if (_bp != null) BtcBpPreset.ToggleLimit(_bp); else BtcWorkbench.ToggleLimit(SelectedStation()); Refresh(M_OPSEL | M_OPQ); }
            catch (Exception e) { BtcLog.Error("GameHud", "rack limit: " + e.Message); }
        }

        private static void ActHelpToggle()
        {
            try { if (_bp != null) BtcBpPreset.ToggleHelp(_bp); else BtcWorkbench.ToggleHelp(SelectedStation()); Refresh(M_OPSEL | M_OPQ); }
            catch (Exception e) { BtcLog.Error("GameHud", "helper: " + e.Message); }
        }

        /// BTC bind label for a button corner ("" while unbound)
        private static Func<string> BindText(string id)
        {
            return delegate { return BtcHotkeys.GetKeys(id, NoKeys).Length == 0 ? "" : BtcHotkeys.ComboText(id); };
        }

        private static readonly KeyCode[] NoKeys = new KeyCode[0];
        private static string[] _partBinds;

        /// CameraBookmarkHotkey per-frame handlers: Crafting stations binds act on the selected station (Better Workbenches)
        internal static void CraftBindsTick()
        {
            if (!BtcWorkbench.On) return;
            CraftingStation s = SelectedStation();
            if (s == null) return;
            try
            {
                if (_op != s) UpdateOp();
                if (_partBinds == null) { _partBinds = new string[6]; for (int i = 0; i < 6; i++) _partBinds[i] = "craft_part_" + (i + 1); }
                for (int i = 0; i < _partBinds.Length; i++)
                {
                    KeyCode[] k = BtcHotkeys.GetKeys(_partBinds[i], NoKeys);
                    if (k.Length != 0 && BtcHotkeys.ComboPressed(k) && i < _opParts.Length) { ActQueuePart(i); BtcSfx.Click(); }
                }
                KeyCode[] r = BtcHotkeys.GetKeys("craft_rack_toggle", NoKeys);
                if (r.Length != 0 && BtcHotkeys.ComboPressed(r) && _opParts.Length > 0) { ActRackToggle(); BtcSfx.Click(); }
                KeyCode[] h = BtcHotkeys.GetKeys("craft_help_toggle", NoKeys);
                if (h.Length != 0 && BtcHotkeys.ComboPressed(h)) { ActHelpToggle(); BtcSfx.Click(); }
            }
            catch (Exception e) { BtcLog.Error("GameHud", "craft binds: " + e.Message); }
        }

        private static RecipeInfo[] _opParts = new RecipeInfo[0];

        private static bool HasParts() { return OpOn() && _opParts.Length > 0 && BtcWorkbench.On; }

        private static RecipeInfo[] PartsOf(RSlot[] slots)
        {
            List<RecipeInfo> a = new List<RecipeInfo>();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Infos.Length > 1) a.AddRange(slots[i].Infos);
            return a.ToArray();
        }

        private static void ActQueuePart(int p)
        {
            try
            {
                if (_bp != null) { if (p >= 0 && p < _opParts.Length && BtcWorkbench.On) BtcBpPreset.Add(_bp, new[] { _opParts[p] }); return; }
                CraftingStation s = SelectedStation();
                if (s == null || _op == null || p < 0 || p >= _opParts.Length || !BtcWorkbench.On) return;
                StationCmd().CallCmdcraftingQueueRecipe(s.gameObject, _opParts[p].id, 1);
            }
            catch (Exception e) { BtcLog.Error("GameHud", "queue part: " + e.Message); }
        }

        private static Sprite SlotSprite(RSlot r)
        {
            if (r.Worm) return BtcWormWardLook.Icon64() ?? Ico("_Sentinel");
            return Ico(r.Icon);
        }

        private static Sec NeedSec()
        {
            return new Sec(M_OPSEL | M_OPQ | M_OPCONTENT, HasNeed, "##operable_resources_requirements", delegate(Sec s)
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

        // Queue stacks: runs of the same order next to each other are one slot with "xN" above the icon (the real order stays visible).
        private static readonly RecipeInfo[] _qRec = new RecipeInfo[BtcWorkbench.Limit];
        private static readonly int[] _qStart = new int[BtcWorkbench.Limit], _qCount = new int[BtcWorkbench.Limit];
        private static int _qGroups;

        private static void BuildStacks()
        {
            _qGroups = 0;
            if (!HasQueue()) return;
            List<RecipeInfo> q = OpQueue();
            for (int i = 0; i < q.Count && _qGroups < _qRec.Length; i++)
            {
                if (_qGroups > 0 && q[i] == _qRec[_qGroups - 1]) { _qCount[_qGroups - 1]++; continue; }
                _qRec[_qGroups] = q[i]; _qStart[_qGroups] = i; _qCount[_qGroups] = 1;
                _qGroups++;
            }
        }

        /// click: remove one order of the stack (its last one, so a running first order is kept); Shift+click: the whole stack
        private static void ActRemoveStack(int g)
        {
            if (_bp != null && g < _qGroups)
            {
                bool every = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                BtcBpPreset.RemoveRange(_bp, every ? _qStart[g] : _qStart[g] + _qCount[g] - 1, every ? _qCount[g] : 1);
                return;
            }
            CraftingStation s = SelectedStation();
            if (s == null || g >= _qGroups) return;
            int start = _qStart[g], n = _qCount[g];
            bool all = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            int first = all ? start : start + n - 1;
            for (int i = start + n - 1; i >= first; i--) StationCmd().CallCmdcraftingRemoveAt(s.gameObject, i);   // back to front: indices stay valid
        }

        /// One queue row, as wide as its stacks up to 8 slots (vanilla width); more stacks scroll inside it (hold LMB and drag, or the wheel).
        private sealed class Strip : Cell
        {
            internal readonly Sec Inner = new Sec(0, Always, null, delegate { });
            private ScrollRect _sr;
            private LayoutElement _le;
            private RectTransform _vrt;
            private int _shown = -1;

            private static float WidthFor(int slots) { slots = Mathf.Clamp(slots, 1, 8); return slots * 48f + (slots - 1) * 1f; }

            internal override void Create(Sec s)
            {
                float w = WidthFor(8);
                GameObject vp = new GameObject("QueueStrip", typeof(RectTransform));
                RectTransform vrt = (RectTransform)vp.transform;
                _vrt = vrt;
                vrt.SetParent(s.Root, false);
                vrt.sizeDelta = new Vector2(w, 48f);
                LayoutElement le = vp.AddComponent<LayoutElement>();
                _le = le;
                le.minWidth = le.preferredWidth = w; le.minHeight = le.preferredHeight = 48f;
                Image bg = vp.AddComponent<Image>();
                bg.color = BtcGhKit.Clear;   // drag target between the slots
                vp.AddComponent<RectMask2D>();

                GameObject ct = new GameObject("Content", typeof(RectTransform));
                RectTransform crt = (RectTransform)ct.transform;
                crt.SetParent(vrt, false);
                crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(0f, 1f); crt.pivot = new Vector2(0f, 0.5f);
                crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(w, 0f);
                HorizontalLayoutGroup g = ct.AddComponent<HorizontalLayoutGroup>();
                g.spacing = 1f; g.childAlignment = TextAnchor.MiddleLeft;
                g.childControlWidth = true; g.childControlHeight = true; g.childForceExpandWidth = false; g.childForceExpandHeight = false;
                ContentSizeFitter f = ct.AddComponent<ContentSizeFitter>();
                f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                _sr = vp.AddComponent<ScrollRect>();
                _sr.horizontal = true; _sr.vertical = false;
                _sr.movementType = ScrollRect.MovementType.Clamped;
                _sr.inertia = true; _sr.decelerationRate = 0.1f; _sr.scrollSensitivity = 25f;
                _sr.viewport = vrt; _sr.content = crt;
                vp.AddComponent<StripDragEnd>();
                Inner.Root = crt;
                Go = vp;
            }

            internal override void Refresh()
            {
                bool v = Visible == null || Visible();
                if (Go.activeSelf != v) Go.SetActive(v);
                if (!v) return;
                BuildStacks();
                int slots = Mathf.Clamp(_qGroups, 1, 8);
                if (slots != _shown)
                {
                    _shown = slots;
                    float w = WidthFor(slots);
                    _le.minWidth = _le.preferredWidth = w;
                    _vrt.sizeDelta = new Vector2(w, 48f);
                }
                Inner.RefreshContent();
            }
        }

        /// Next to the strip's ScrollRect: a short press-drag-release is still a click on the slot (BtcGhButton.EndDrag).
        internal sealed class StripDragEnd : MonoBehaviour, UnityEngine.EventSystems.IEndDragHandler
        {
            public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e) { BtcGhButton.EndDrag(e); }
        }

        private static Sec QueueSec()
        {
            return new Sec(M_OPSEL | M_OPQ | M_OPLOOP, HasQueue, "##operable_queue", delegate(Sec s)
            {
                Strip strip = new Strip { Visible = HasQueue };
                for (int i = 0; i < BtcWorkbench.Limit; i++) // Data.Operable:GetQueueSize() is the constant 8; Better Workbenches scrolls the rest
                {
                    int k = i;   // stack index
                    strip.Inner.Add(new Btn
                    {
                        Visible = delegate { return _qGroups > k; },
                        IconFn = delegate { return _qGroups > k ? RecipeSprite(_qRec[k]) : null; },
                        IconColor = Color.white, IconSize = 64f,
                        CountFn = delegate { return _qGroups > k && _qCount[k] > 1 ? "x" + _qCount[k] : ""; },
                        CountAlign = TextAnchor.UpperCenter, CountSize = 12, CountColor = BtcGhKit.Yellow,
                        Tip = delegate { return _qGroups > k && _qCount[k] > 1 ? BtcLang.Tr("click: remove one, Shift+click: remove the whole stack") : null; },
                        OnAction = delegate { ActRemoveStack(k); },
                        Highlight = k == 0 ? (Func<int>)delegate { return 15; } : null,
                        DeferOnDrag = true,
                    });
                }
                s.Add(strip);
                s.Add(new InnerDiv(HasQueue));
                s.Add(new Btn { Visible = HasQueue, IconName = "_UI_Wheel", IconSize = 32f, IconColorFn = LoopColor, HasHotkey = true, Hotkey = Key("Projects", "project_ToggleLoop"), OnAction = ActLoop });
                s.Add(new Btn { Visible = HasQueue, IconName = "_UI_X", IconSize = 32f, IconColor = BtcGhKit.Red, HasHotkey = true, Hotkey = Key("Projects", "project_ClearQueue"), OnAction = ActClear });
            });
        }

        private static Color LoopColor() { return OpLoop() ? BtcGhKit.Red : BtcGhKit.Gray; }

        // ------------------------------------------------------------------ actions (CraftingStation.lua_AddToQueue / RemoveFromQueue / ClearQueue / SetIsQueueLooping)
        private static UNetProjectCmd StationCmd() { return User.LocalUser.GetComponent<UNetProjectCmd>(); }

        /// _m.bh.operableRecipes[i].OnAction: only while that slot is visible.
        internal static void ActQueueSlot(int slot)
        {
            try
            {
                if (_bp != null) { if (slot >= 1 && slot <= _opSlots.Length) BtcBpPreset.Add(_bp, _opSlots[slot - 1].Infos); return; }
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
            if (_bp != null) { BtcBpPreset.ClearQueue(_bp); return; }
            CraftingStation s = SelectedStation();
            if (s != null) StationCmd().CallCmdcraftingDoNothing(s.gameObject);
        }

        internal static void ActLoop()
        {
            if (_bp != null) { BtcBpPreset.ToggleLoop(_bp); return; }
            CraftingStation s = SelectedStation();
            if (s != null) StationCmd().CallCmdcraftingLoop(s.gameObject, !s.Looping);
        }
    }
}
