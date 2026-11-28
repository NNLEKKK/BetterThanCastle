using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Brix.Assets;
using Brix.Game;
using Brix.Game.Components;
using AdjectiveInfo = Brix.Game.AI.AdjectiveInfo;
using Brix.Game.Components.Validators.Graphs;
using Brix.Game.Network;
using Brix.Game.Semantique;
using Brix.Legacy;
using Brix.Network;
using Brix.UI.GameSelector;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace BetterThanCastle
{
    /// <summary>
    /// Advanced blueprints: every own blueprint of a stockpile, a tool rack or a crafting station carries a preset (pie menu on the blue mark):
    /// stockpile = vanilla resource filter (all / none / only X), rack = kit filter (BtcRackFilter), station = whole crafting queue + loop +
    /// Better Workbenches rack limit / helper. When the blueprint is built the host applies the preset to the new building (vanilla command
    /// bodies of the owner, so the vanilla sync and the replay recording see a normal player order).
    /// Presets travel with compound blueprints: graph nodes are keyed by "asset name + relative position"; saved compounds keep them in
    /// btc-presets.txt inside the compound folder; placing a compound tags the new blueprints by position (like BtcWormWardBuild).
    /// Only the author of a blueprint edits it (affiliation.UserCreator; unknown creator after a save/rejoin = same faction).
    /// Personal setting Orders "bp_presets" (default on) only hides the pie menus; the host always applies stored presets.
    /// Wire (BC ON): 146 client->host "netId=payload" (empty payload = clear), 147 host->clients "netId=payload;..." (own faction only).
    /// OFF: no handlers, no sends, no pie branch.
    /// Payload: "S*" accept all, "S-" accept none, "S:Code" only resource Code; "R&lt;kit&gt;" rack kit; "C&lt;loop&gt;,&lt;limit&gt;,&lt;help&gt;,Name+Name+..."
    /// (limit/help -1 = owner default; names = RecipeInfo.Creates.Name). Save sidecar btc-bppresets.txt "x y z assetName payload".
    /// </summary>
    internal static partial class BtcBpPreset
    {
        internal const short MsgSet = 146, MsgState = 147;
        internal const string FileName = "btc-bppresets.txt", CompoundFile = "btc-presets.txt";
        private const int MaxQueue = 40;

        internal const int KStock = 1, KRack = 2, KCraft = 3;

        private static readonly Dictionary<uint, string> Presets = new Dictionary<uint, string>();     // blueprint netId -> payload (host: all, client: own faction)
        private static readonly Dictionary<int, string> ByTarget = new Dictionary<int, string>();       // compound preview copy instance id -> payload
        private static readonly Dictionary<BlueprintGraph, Dictionary<string, string>> ByGraph = new Dictionary<BlueprintGraph, Dictionary<string, string>>();
        private static readonly List<Pend> Pending = new List<Pend>();     // local: compound placement waiting for its blueprints
        private static readonly List<Apply> Applies = new List<Apply>();   // host: built objects waiting for their components
        private static bool _dirty;
        private static float _nextSend, _nextPrune;
        private static string _clip;

        private struct Pend { internal Vector3 Pos; internal string Name; internal string Payload; internal float T; }
        private sealed class Apply { internal GameObject Go; internal string Payload; internal Faction F; internal GameObject Owner; internal float T, Until; }

        internal static bool On { get { return BtcNetworkGate.Enabled; } }
        internal static bool LocalOn { get { return On && BtcHotkeys.GetBool("bp_presets", true); } }

        internal static void Clear() { Presets.Clear(); ByTarget.Clear(); ByGraph.Clear(); Pending.Clear(); Applies.Clear(); _pending = null; _pieBp = null; _hover = null; }

        // ------------------------------------------------------------------ kinds
        internal sealed class Slot
        {
            internal string Icon, En, Ru;
            internal Func<RecipeInfo[]> Infos;
            internal Func<bool> Visible;
        }

        private static Slot S(string icon, string en, string ru, Func<RecipeInfo[]> infos) { return new Slot { Icon = icon, En = en, Ru = ru, Infos = infos }; }

        internal static readonly string[] StationNames = { "Furnace", "ChoppingBlock", "Loom", "Workbench", "Forge", "MachineShop", "Lab", "Altar" };
        internal static readonly string[] StationIcons = { "_Furnace", "_Chopping_Block", "_Loom", "_Workbench", "_Forge", "_Machine_Shop", "_Lab", "_Altar" };
        private static Slot[][] _slots;

        /// Same slots as the C# station panel (BtcGhPopupOp.SlotsOf): a kit slot queues all its parts.
        internal static Slot[] SlotsOf(int k)
        {
            if (_slots == null)
            {
                _slots = new Slot[8][];
                _slots[0] = new[] { S("_Iron", "Iron ingot", "Слиток железа", () => new[] { CookBook.IronIngot }), S("_Glass_Ingot", "Glass", "Стекло", () => new[] { CookBook.GlassIngot }) };
                _slots[1] = new[] { S("_Plank", "Planks", "Доски", () => new[] { CookBook.Planks }) };
                _slots[2] = new[] { S("_Fabric", "Fabric", "Ткань", () => new[] { CookBook.FabricPile }), S("_Rope", "Rope", "Верёвка", () => new[] { CookBook.RopeBallot }) };
                _slots[3] = new[] { S("_Halberdier_Kit", "Halberdier kit", "Набор алебардиста", () => new[] { CookBook.Poleaxe, CookBook.KettleHat }),
                    S("_Archer_Kit", "Archer kit", "Набор лучника", () => new[] { CookBook.Bow, CookBook.Quiver, CookBook.Cap }), S("_Lantern", "Lantern ward", "Вард-фонарь", () => new[] { CookBook.LanternWard }) };
                _slots[4] = new[] { S("_Knight_Kit", "Knight kit", "Набор рыцаря", () => new[] { CookBook.Sword, CookBook.Shield, CookBook.Helmet }), S("_Cog", "Cog", "Шестерня", () => new[] { CookBook.Cog }) };
                _slots[5] = new[] { S("_Arbalist_Kit", "Arbalist kit", "Набор арбалетчика", () => new[] { CookBook.CrossbowWeapon, CookBook.CrossbowPouch, CookBook.CrossbowMorion }),
                    S("_Beartrap", "Bear trap", "Капкан", () => new[] { CookBook.BearTrap }) };
                _slots[6] = new[] { S("_Alchemist_Kit", "Alchemist kit", "Набор алхимика", () => new[] { CookBook.AlchemistPouch, CookBook.AlchemistHat }), S("_Bomb", "Bomb", "Бомба", () => new[] { CookBook.Bomb }),
                    S("_Explosive_Barrel", "Explosive barrel", "Взрывная бочка", () => new[] { CookBook.ExplosiveBarrel }) };
                Slot worm = S(BtcWormWardLook.IconName, "Wormhole ward", "Вард-червоточина", () => BtcWormWardCraft.WormRecipe != null ? new[] { BtcWormWardCraft.WormRecipe } : new RecipeInfo[0]);
                worm.Visible = () => BtcWormWard.RuleOn && BtcWormWardCraft.WormRecipe != null;
                _slots[7] = new[] { S("_Artificer_Kit", "Artificer kit", "Набор артифисера", () => new[] { CookBook.Staff, CookBook.Sachel, CookBook.ArtificerHat }),
                    S("_Sentinel", "Sentinel ward", "Вард-страж", () => new[] { CookBook.SentinelWard }), worm, S("_Healing_Ward", "Healing ward", "Лечащий вард", () => new[] { CookBook.HealingWard }),
                    S("_Capture_Stone", "Capture ward", "Вард захвата", () => new[] { CookBook.CaptureWard }), S("_Pylon", "Pylon ward", "Вард-пилон", () => new[] { CookBook.PylonWard }) };
            }
            return k >= 0 && k < _slots.Length ? _slots[k] : new Slot[0];
        }

        private static string NameOfAsset(Blueprint b) { return b != null && b.AssetKey != null ? b.AssetKey.Name : null; }

        /// KStock / KRack / KCraft (station index in craft), 0 = no preset for this blueprint.
        internal static int KindOf(string asset, out int craft)
        {
            craft = -1;
            if (string.IsNullOrEmpty(asset)) return 0;
            if (asset == "Pallet") return KStock;
            if (asset == "Toolrack" || asset == "SingleToolrack") return KRack;
            craft = Array.IndexOf(StationNames, asset);
            return craft >= 0 ? KCraft : 0;
        }

        // ------------------------------------------------------------------ payload helpers
        private sealed class Craft { internal bool Loop; internal int Limit = -1, Help = -1; internal List<string> Q = new List<string>(); }

        private static Craft ParseCraft(string p)
        {
            Craft c = new Craft();
            if (string.IsNullOrEmpty(p) || p[0] != 'C') return c;
            string[] t = p.Substring(1).Split(',');
            if (t.Length < 4) return c;
            c.Loop = t[0] == "1";
            int.TryParse(t[1], out c.Limit);
            int.TryParse(t[2], out c.Help);
            if (t[3].Length > 0) c.Q.AddRange(t[3].Split('+'));
            return c;
        }

        private static string Encode(Craft c)
        {
            if (!c.Loop && c.Limit < 0 && c.Help < 0 && c.Q.Count == 0) return string.Empty;
            return "C" + (c.Loop ? 1 : 0) + "," + c.Limit + "," + c.Help + "," + string.Join("+", c.Q.ToArray());
        }

        private static Dictionary<string, RecipeInfo> _byName;

        private static RecipeInfo RecipeByName(string n)
        {
            if (_byName == null || (BtcWormWardCraft.WormRecipe != null && !_byName.ContainsValue(BtcWormWardCraft.WormRecipe)))
            {
                Dictionary<string, RecipeInfo> m = new Dictionary<string, RecipeInfo>();
                for (int k = 0; k < StationNames.Length; k++)
                {
                    Slot[] s = SlotsOf(k);
                    for (int i = 0; i < s.Length; i++)
                        foreach (RecipeInfo r in s[i].Infos())
                            if (r != null && r.Creates != null && !m.ContainsKey(r.Creates.Name)) m[r.Creates.Name] = r;
                }
                _byName = m;
            }
            RecipeInfo ri;
            return n != null && _byName.TryGetValue(n, out ri) ? ri : null;
        }

        // ------------------------------------------------------------------ lookup
        internal static uint KeyOf(Blueprint b)
        {
            NetworkIdentity ni = b != null ? b.GetComponent<NetworkIdentity>() : null;
            return ni != null ? ni.netId.Value : 0u;
        }

        internal static string Get(Blueprint b)
        {
            uint k = KeyOf(b);
            string p;
            return k != 0u && Presets.TryGetValue(k, out p) ? p : null;
        }

        /// payload of a live blueprint or of a compound preview copy
        private static string PayloadOfGo(GameObject go)
        {
            if (go == null) return null;
            Blueprint b = go.GetComponent<Blueprint>();
            if (KeyOf(b) != 0u) return Get(b);
            string p;
            return ByTarget.TryGetValue(go.GetInstanceID(), out p) ? p : null;
        }

        /// the author may edit (unknown creator: same faction)
        internal static bool IsMine(Blueprint b)
        {
            User u = User.LocalUser;
            return b != null && u != null && Mine(b, u);
        }

        private static bool Mine(Blueprint b, User u)
        {
            if (b == null || u == null || u.faction == null || !ReferenceEquals(b.faction, u.faction)) return false;
            GameObject c = b.affiliation != null ? b.affiliation.UserCreator : null;
            return c == null || c == u.gameObject;
        }

        // ------------------------------------------------------------------ hover + pie target
        private static Blueprint _hover, _pieBp;
        private static float _hoverT;

        /// UISelectable.OnHover / OnUnhover postfix
        internal static void OnHover(UISelectable s, bool on)
        {
            if (s == null) return;
            Blueprint b = s.GetComponentInParent<Blueprint>();
            if (b == null) { if (on) _hover = null; return; }
            if (on) {
#if DEV
                if (!ReferenceEquals(_hover, b)) BtcLog.Info("BpPreset", "hover mark " + NameOfAsset(b));
#endif
                _hover = b; _hoverT = Time.unscaledTime; }
            else if (ReferenceEquals(_hover, b)) _hover = null;
        }

        private static Blueprint Live(Blueprint b) { return b != null && b.gameObject.activeInHierarchy && KeyOf(b) != 0u ? b : null; }

        /// PieContenu.Menu.TestHover postfix for our tags BtcBpStock / BtcBpRack / BtcBpCraft0..7
        internal static bool TestHover(object objet, string tag)
        {
            if (!LocalOn || tag == null || !tag.StartsWith("BtcBp", StringComparison.Ordinal)) return false;
            Blueprint b = Live(_hover);
            if (b == null)
            {
                GameObject go = objet as GameObject;
                if (go == null && objet is Component) go = ((Component)objet).gameObject;
                b = go != null ? Live(go.GetComponentInParent<Blueprint>()) : null;
            }
            if (b == null || !IsMine(b)) return false;
            int craft;
            int kind = KindOf(NameOfAsset(b), out craft);
            bool hit = (kind == KStock && tag == "BtcBpStock") || (kind == KRack && tag == "BtcBpRack") || (kind == KCraft && tag == "BtcBpCraft" + craft);
            if (hit) _pieBp = b;
            return hit;
        }

        private static Blueprint PieBp() { Blueprint b = Live(_pieBp); return b != null && IsMine(b) ? b : null; }

        // ------------------------------------------------------------------ pie conditions / events (Instructions.Batire postfix)
        internal static void Install()
        {
            Instructions.conditions["Condition_BtcBp_Any"] = () => PieBp() != null;
            Instructions.conditions["Condition_BtcBp_Has"] = () => !string.IsNullOrEmpty(Get(PieBp()));
            Instructions.commendes["Event_BtcBp_Clear"] = () => Edit(p => string.Empty);
            Instructions.commendes["Event_BtcBp_Copy"] = () => { Blueprint b = PieBp(); if (b != null) { _clip = Get(b) ?? string.Empty; BtcLog.Info("BpPreset", "copied '" + _clip + "'"); } };
            Instructions.conditions["Condition_BtcBp_CanPaste"] = () =>
            {
                Blueprint b = PieBp();
                return b != null && _clip != null && _clip.Length > 0 && _clip[0] == KindChar(b) && _clip != (Get(b) ?? string.Empty);
            };
            Instructions.commendes["Event_BtcBp_Paste"] = () => { if (_clip != null) Edit(p => _clip); };

            // stockpile
            Instructions.conditions["Condition_BtcBp_NotAll"] = () => PieBp() != null && Get(PieBp()) != "S*";
            Instructions.conditions["Condition_BtcBp_NotNone"] = () => PieBp() != null && Get(PieBp()) != "S-";
            Instructions.commendes["Event_BtcBp_StAll"] = () => Edit(p => "S*");
            Instructions.commendes["Event_BtcBp_StNone"] = () => Edit(p => "S-");
            foreach (AdjectiveInfo adj in AdjectiveInfo.GetInfosFromType(AdjectiveInfo.AdjectiveType.Ressource))
            {
                string code = adj.codeName;
                Instructions.conditions["Condition_BtcBp_NotOnly_" + code] = () => PieBp() != null && Get(PieBp()) != "S:" + code;
                Instructions.commendes["Event_BtcBp_Only_" + code] = () => Edit(p => "S:" + code);
            }

            // rack
            Instructions.conditions["Condition_BtcBp_RackNot_Any"] = () => PieBp() != null && !string.IsNullOrEmpty(Get(PieBp()));
            Instructions.commendes["Event_BtcBp_Rack_Any"] = () => Edit(p => string.Empty);
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++)
            {
                string v = "R" + i;
                Instructions.conditions["Condition_BtcBp_RackNot_" + BtcRackFilter.KitNames[i]] = () => PieBp() != null && Get(PieBp()) != v;
                Instructions.commendes["Event_BtcBp_Rack_" + BtcRackFilter.KitNames[i]] = () => Edit(p => v);
            }

            // stations
            Instructions.conditions["Condition_BtcBp_CanAdd"] = () => PieBp() != null && ParseCraft(Get(PieBp())).Q.Count < Math.Min(MaxQueue, BtcWorkbench.Max());
            Instructions.conditions["Condition_BtcBp_HasQueue"] = () => PieBp() != null && ParseCraft(Get(PieBp())).Q.Count > 0;
            Instructions.conditions["Condition_BtcBp_Loop"] = () => PieBp() != null && ParseCraft(Get(PieBp())).Loop;
            Instructions.conditions["Condition_BtcBp_NoLoop"] = () => PieBp() != null && !ParseCraft(Get(PieBp())).Loop;
            Instructions.commendes["Event_BtcBp_Loop"] = () => EditCraft(c => c.Loop = !c.Loop);
            Instructions.commendes["Event_BtcBp_RemoveLast"] = () => EditCraft(RemoveLast);
            Instructions.commendes["Event_BtcBp_Open"] = () => OpenPanel(PieBp());
            Instructions.conditions["Condition_BtcBp_LimitOn"] = () => BtcWorkbench.On && PieBp() != null && EffLimit(ParseCraft(Get(PieBp())));
            Instructions.conditions["Condition_BtcBp_LimitOff"] = () => BtcWorkbench.On && PieBp() != null && !EffLimit(ParseCraft(Get(PieBp())));
            Instructions.commendes["Event_BtcBp_Limit"] = () => EditCraft(c => c.Limit = EffLimit(c) ? 0 : 1);
            Instructions.conditions["Condition_BtcBp_HelpOn"] = () => BtcWorkbench.On && HelperKind() && EffHelp(ParseCraft(Get(PieBp())));
            Instructions.conditions["Condition_BtcBp_HelpOff"] = () => BtcWorkbench.On && HelperKind() && !EffHelp(ParseCraft(Get(PieBp())));
            Instructions.commendes["Event_BtcBp_Help"] = () => EditCraft(c => c.Help = EffHelp(c) ? 0 : 1);
            for (int k = 0; k < StationNames.Length; k++)
            {
                Slot[] s = SlotsOf(k);
                for (int i = 0; i < s.Length; i++)
                {
                    Slot slot = s[i];
                    Instructions.conditions["Condition_BtcBp_Q" + k + "_" + i] = () => (slot.Visible == null || slot.Visible())
                        && PieBp() != null && ParseCraft(Get(PieBp())).Q.Count + slot.Infos().Length <= Math.Min(MaxQueue, BtcWorkbench.Max());
                    Instructions.commendes["Event_BtcBp_Q" + k + "_" + i] = () => EditCraft(c =>
                    {
                        foreach (RecipeInfo r in slot.Infos()) if (r != null && r.Creates != null) c.Q.Add(r.Creates.Name);
                    });
                }
            }
        }

        private static char KindChar(Blueprint b)
        {
            int craft;
            switch (KindOf(NameOfAsset(b), out craft)) { case KStock: return 'S'; case KRack: return 'R'; case KCraft: return 'C'; }
            return '?';
        }

        private static bool EffLimit(Craft c) { return c.Limit >= 0 ? c.Limit == 1 : BtcPlayerPrefs.LocalRackLimit; }
        private static bool EffHelp(Craft c) { return c.Help >= 0 ? c.Help == 1 : BtcPlayerPrefs.LocalHelpers; }

        /// stations that may have a helper (BtcWorkbench.HelperStation: not the chopping block)
        private static bool HelperKind() { return HelperKind(PieBp()); }

        internal static bool HelperKind(Blueprint b)
        {
            int craft;
            return b != null && KindOf(NameOfAsset(b), out craft) == KCraft && craft != 1;
        }

        private static void RemoveLast(Craft c) { RemoveLast(PieBp(), c); }

        private static void RemoveLast(Blueprint b, Craft c)
        {
            if (c.Q.Count == 0) return;
            string last = c.Q[c.Q.Count - 1];
            int craft;
            Slot[] s = SlotsOf(KindOf(NameOfAsset(b), out craft) == KCraft ? craft : -1);
            for (int i = 0; i < s.Length; i++)
            {
                RecipeInfo[] inf = s[i].Infos();
                if (inf.Length == 0 || inf[inf.Length - 1] == null || inf[inf.Length - 1].Creates.Name != last || c.Q.Count < inf.Length) continue;
                bool all = true;
                for (int j = 0; j < inf.Length; j++) if (c.Q[c.Q.Count - inf.Length + j] != inf[j].Creates.Name) { all = false; break; }
                if (all) { c.Q.RemoveRange(c.Q.Count - inf.Length, inf.Length); return; }
            }
            c.Q.RemoveAt(c.Q.Count - 1);
        }

        private static void EditCraft(Action<Craft> a) { EditCraft(PieBp(), a); }

        private static void EditCraft(Blueprint b, Action<Craft> a)
        {
            EditOn(b, p =>
            {
                Craft c = ParseCraft(p);
                a(c);
                return Encode(c);
            });
        }

        private static void Edit(Func<string, string> f) { EditOn(PieBp(), f); }

        private static void EditOn(Blueprint b, Func<string, string> f)
        {
            if (b == null || !On || !IsMine(b)) return;
            try
            {
                string old = Get(b) ?? string.Empty;
                string v = f(old) ?? string.Empty;
                if (v == old) return;
                Set(b, v);
                BtcSfx.Click();
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "Edit: " + e); }
        }

        // ------------------------------------------------------------------ station panel of a mark (UI/GameHud/BtcGhPopupOp.cs)
        private static Blueprint _selBp;
        private static string _qSrc;
        private static readonly List<RecipeInfo> _q = new List<RecipeInfo>();

        /// UISelectable.OnSelect / OnUnselect postfix: the last selected own station mark
        internal static void OnSelect(UISelectable s, bool on)
        {
            if (s == null) return;
            Blueprint b = s.GetComponentInParent<Blueprint>();
            Blueprint before = SelectedStation;
            if (on)
            {
                int craft;
                if (b != null && KindOf(NameOfAsset(b), out craft) == KCraft && IsMine(b)) _selBp = b;
                if (!ReferenceEquals(b, _openBp)) _openBp = null;   // any other selection closes a mark panel opened by click / pie
            }
            else if (b != null && ReferenceEquals(_selBp, b)) _selBp = null;
            if (!ReferenceEquals(before, SelectedStation)) BtcGhPopup.OnBpChanged(true);
        }

        private static Blueprint _openBp;   // opened in build mode (LMB on the mark or pie button): no vanilla selection there

        /// open the station panel of this mark (build mode: LMB on the mark, or the pie button)
        internal static void OpenPanel(Blueprint b)
        {
            if (b == null || !IsMine(b) || StationKind(b) < 0) return;
            _openBp = b;
            BtcSfx.Click();
            BtcLog.Info("BpPreset", "station panel of mark " + KeyOf(b) + " " + NameOfAsset(b));
            BtcGhPopup.OnBpChanged(true);
        }

        /// BlueprintPlacementPicker.PlaceBlueprint prefix (build mode click): false = the click was on an own station mark, its panel opens
        internal static bool ClickPlace()
        {
            if (!LocalOn) return true;
            Blueprint b = MarkUnderCursor(true);
            if (b == null) { b = Live(_hover); if (b != null && (StationKind(b) < 0 || !IsMine(b))) b = null; }
            if (b == null) return true;
            OpenPanel(b);
            return false;
        }

        /// the selected own station mark (C# station panel shows its preset), or null
        internal static Blueprint SelectedStation
        {
            get
            {
                if (!LocalOn) return null;
                Blueprint o = Live(_openBp);
                if (o != null && IsMine(o)) return o;
                Blueprint b = Live(_selBp);
                return b != null && IsMine(b) && UIGameSelector.IsSelected(b.gameObject) ? b : null;
            }
        }

        /// station index (BtcBpPreset.StationNames = BtcGhPopup kinds), -1 if not a station mark
        internal static int StationKind(Blueprint b)
        {
            int craft;
            return KindOf(NameOfAsset(b), out craft) == KCraft ? craft : -1;
        }

        internal static List<RecipeInfo> Queue(Blueprint b)
        {
            string p = Get(b) ?? string.Empty;
            if (p == _qSrc) return _q;
            _qSrc = p;
            _q.Clear();
            Craft c = ParseCraft(p);
            for (int i = 0; i < c.Q.Count; i++) { RecipeInfo r = RecipeByName(c.Q[i]); if (r != null) _q.Add(r); }
            return _q;
        }

        internal static bool Loop(Blueprint b) { return ParseCraft(Get(b)).Loop; }
        internal static bool LimitOn(Blueprint b) { return EffLimit(ParseCraft(Get(b))); }
        internal static bool HelpOn(Blueprint b) { return EffHelp(ParseCraft(Get(b))); }

        internal static void Add(Blueprint b, RecipeInfo[] infos)
        {
            EditCraft(b, c =>
            {
                int max = Math.Min(MaxQueue, BtcWorkbench.Max());
                if (c.Q.Count + infos.Length > max) return;
                foreach (RecipeInfo r in infos) if (r != null && r.Creates != null) c.Q.Add(r.Creates.Name);
            });
        }

        internal static void RemoveRange(Blueprint b, int start, int n)
        {
            EditCraft(b, c => { if (start >= 0 && n > 0 && start + n <= c.Q.Count) c.Q.RemoveRange(start, n); });
        }

        internal static void ClearQueue(Blueprint b) { EditCraft(b, c => { c.Q.Clear(); c.Loop = false; }); }
        internal static void ToggleLoop(Blueprint b) { EditCraft(b, c => c.Loop = !c.Loop); }
        internal static void ToggleLimit(Blueprint b) { EditCraft(b, c => c.Limit = EffLimit(c) ? 0 : 1); }
        internal static void ToggleHelp(Blueprint b) { if (HelperKind(b)) EditCraft(b, c => c.Help = EffHelp(c) ? 0 : 1); }

        /// local entry: host sets and sends, a client sets at once and asks the host
        private static void Set(Blueprint b, string v)
        {
            uint key = KeyOf(b);
            if (key == 0u) return;
            Store(key, v);
            BtcLog.Info("BpPreset", "blueprint " + key + " " + NameOfAsset(b) + " = '" + v + "'");
            if (!NetworkServer.active) Send(key, v);
        }

        private static void Store(uint key, string v)
        {
            if (string.IsNullOrEmpty(v)) Presets.Remove(key); else Presets[key] = v;
            BtcGhPopup.OnBpChanged(false);
            _dirty = true;
        }

        // ------------------------------------------------------------------ net
        internal static void RegisterHandlers(NetworkClient clientOverride)
        {
            Presets.Clear(); ByTarget.Clear(); Pending.Clear(); Applies.Clear(); _pieBp = null; _hover = null;   // netIds of another match
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                NetworkClient c = clientOverride ?? (m != null ? m.LocalNetworkClient : null);
                if (c != null && !NetworkServer.active)
                {
                    c.UnregisterHandler(MsgState);
                    c.RegisterHandler(MsgState, OnStateAtClient);
                }
                if (NetworkServer.active)
                {
                    NetworkServer.UnregisterHandler(MsgSet);
                    NetworkServer.RegisterHandler(MsgSet, OnSetAtHost);
                }
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "RegisterHandlers: " + e); }
        }

        private static void Send(uint key, string v)
        {
            if (!BtcNetworkGate.Enabled) return;
            try
            {
                UNetManager m = NetworkManager.singleton as UNetManager;
                if (m != null && m.LocalNetworkClient != null) m.LocalNetworkClient.Send(MsgSet, new StringMessage(key + "=" + (v ?? string.Empty)));
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "Send: " + e.Message); }
        }

        private static bool ValidPayload(string v, int kind)
        {
            if (v.Length == 0) return true;
            if (v.Length > 1200 || v.IndexOf(';') >= 0 || v.IndexOf('=') >= 0 || v.IndexOf(' ') >= 0 || v.IndexOf('\n') >= 0) return false;
            return (kind == KStock && v[0] == 'S') || (kind == KRack && v[0] == 'R') || (kind == KCraft && v[0] == 'C');
        }

        private static void OnSetAtHost(NetworkMessage nm)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || nm.conn == null) return;
                string s = nm.ReadMessage<StringMessage>().value ?? string.Empty;
                int eq = s.IndexOf('=');
                uint key;
                if (eq <= 0 || !uint.TryParse(s.Substring(0, eq), out key)) return;
                string v = s.Substring(eq + 1);
                NetworkIdentity ni;
                if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(key), out ni) || ni == null) return;
                Blueprint b = ni.GetComponent<Blueprint>();
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(nm.conn) : null;
                int craft;
                if (b == null || !Mine(b, u) || !ValidPayload(v, KindOf(NameOfAsset(b), out craft))) return;   // author only
                Store(key, v);
                BtcLog.Info("BpPreset", "client set blueprint " + key + " " + NameOfAsset(b) + " = '" + v + "'");
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnSetAtHost: " + e); }
        }

        private static void OnStateAtClient(NetworkMessage nm)
        {
            try
            {
                string s = nm.ReadMessage<StringMessage>().value ?? string.Empty;
                Presets.Clear();
                foreach (string kv in s.Split(';'))
                {
                    int eq = kv.IndexOf('=');
                    uint key;
                    if (eq > 0 && uint.TryParse(kv.Substring(0, eq), out key) && eq + 1 < kv.Length) Presets[key] = kv.Substring(eq + 1);
                }
                BtcGhPopup.OnBpChanged(false);
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnStateAtClient: " + e); }
        }

        private static string StateFor(Faction f)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<uint, string> kv in Presets)
            {
                NetworkIdentity ni;
                if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(kv.Key), out ni) || ni == null) continue;
                Blueprint b = ni.GetComponent<Blueprint>();
                if (b == null || f == null || !ReferenceEquals(b.faction, f)) continue;   // own faction only (fair fog)
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            }
            return sb.ToString();
        }

        internal static void SendState(NetworkConnection conn)
        {
            if (conn == null || !NetworkServer.active || !BtcNetworkGate.Enabled || BtcNetManager.IsLocalConnection(conn)) return;
            try
            {
                User u = UNetManager.instance != null ? UNetManager.instance.GetUserFromConnection(conn) : null;
                if (u != null) conn.Send(MsgState, new StringMessage(StateFor(u.faction)));
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "SendState: " + e.Message); }
        }

        /// BtcTimer.Tick: host sends on change (and every 5 s while presets exist: rejoin / late join), prunes dead blueprints,
        /// applies presets to built objects, restores presets from a save
        internal static void Tick()
        {
            AnchorTick();
            TasksTick();
            if (!On) return;
            float now = Time.unscaledTime;
            if (Pending.Count > 0)
                for (int i = Pending.Count - 1; i >= 0; i--) if (now - Pending[i].T > 30f) Pending.RemoveAt(i);
            if (!NetworkServer.active) return;
            try
            {
                if (now >= _nextPrune) { _nextPrune = now + 2f; Prune(); }
                if (Applies.Count > 0) ApplyTick(now);
                if (_pending != null) LoadTick(now);
                bool due = Presets.Count > 0 && now >= _nextSend;
                if (_dirty || due)
                {
                    _dirty = false;
                    _nextSend = now + 5f;
                    for (int i = 0; i < NetworkServer.connections.Count; i++)
                    {
                        NetworkConnection c = NetworkServer.connections[i];
                        if (c != null && c.isConnected && !BtcNetManager.IsLocalConnection(c)) SendState(c);
                    }
                }
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "Tick: " + e); }
        }

        private static readonly List<uint> _dead = new List<uint>();

        private static void Prune()
        {
            if (Presets.Count == 0) return;
            _dead.Clear();
            foreach (KeyValuePair<uint, string> kv in Presets)
            {
                NetworkIdentity ni;
                if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(kv.Key), out ni) || ni == null || ni.GetComponent<Blueprint>() == null) _dead.Add(kv.Key);
            }
            for (int i = 0; i < _dead.Count; i++) Presets.Remove(_dead[i]);
            if (_dead.Count > 0) _dirty = true;
        }

        // ------------------------------------------------------------------ built: apply to the new object (host)
        /// Blueprint.MorphToObject prefix: the preset of this blueprint (taken out of the table)
        internal static string BeforeMorph(Blueprint b)
        {
            if (!NetworkServer.active || !On || b == null) return null;
            uint key = KeyOf(b);
            string p;
            if (key == 0u || !Presets.TryGetValue(key, out p)) return null;
            Presets.Remove(key);
            _dirty = true;
            return p;
        }

        internal static void AfterMorph(Blueprint b, string payload, GameObject result)
        {
            if (string.IsNullOrEmpty(payload) || result == null) return;
            GameObject owner = b != null && b.affiliation != null ? b.affiliation.UserCreator : null;
            float now = Time.unscaledTime;
            Applies.Add(new Apply { Go = result, Payload = payload, F = b != null ? b.faction : null, Owner = owner, T = now + 0.3f, Until = now + 20f });
        }

        private static void ApplyTick(float now)
        {
            for (int i = Applies.Count - 1; i >= 0; i--)
            {
                Apply a = Applies[i];
                if (a.Go == null || now > a.Until) { Applies.RemoveAt(i); if (a.Go != null) BtcLog.Warn("BpPreset", "apply timed out: '" + a.Payload + "'"); continue; }
                if (now < a.T || !a.Go.activeInHierarchy) continue;
                bool done;
                try { done = TryApply(a); }
                catch (Exception e) { BtcLog.Error("BpPreset", "apply: " + e); done = true; }
                if (done) Applies.RemoveAt(i); else a.T = now + 0.3f;
            }
        }

        private static UNetProjectCmd OwnerCmd(Apply a)
        {
            UNetProjectCmd c = a.Owner != null ? a.Owner.GetComponent<UNetProjectCmd>() : null;
            if (c != null && Affiliation.IsOwnedBy(a.Go, a.Owner)) return c;
            Faction f = a.F;
            if (f == null) return null;
            for (int i = 0; i < f.OwnerSyncList.Count; i++)
            {
                GameObject g = f.OwnerSyncList[i];
                c = g != null ? g.GetComponent<UNetProjectCmd>() : null;
                if (c != null) return c;
            }
            return null;
        }

        private static bool TryApply(Apply a)
        {
            string p = a.Payload;
            if (NetIdOf(a.Go) == 0u) return false;
            UNetProjectCmd cmd = OwnerCmd(a);
            switch (p[0])
            {
                case 'S':
                    {
                        if (a.Go.GetComponent<Recepteur>() == null || cmd == null) return false;
                        if (p == "S*") cmd.CmdStockpileAcceptAll(a.Go);
                        else if (p == "S-") cmd.CmdStockpileKeepEmpty(a.Go);
                        else if (p.StartsWith("S:")) cmd.CmdStockpileAcceptOnly(a.Go, p.Substring(2));
                        break;
                    }
                case 'R':
                    {
                        ToolRack rack = a.Go.GetComponent<ToolRack>();
                        int kit;
                        if (rack == null || rack.recepteur == null || !int.TryParse(p.Substring(1), out kit)) return rack == null;
                        if (kit < 0 || kit >= BtcRackFilter.KitNames.Length) return true;
                        BtcRackFilter.ApplyLocal(rack, kit);
                        BtcRackFilterNet.Broadcast();
                        break;
                    }
                case 'C':
                    {
                        CraftingStation st = a.Go.GetComponent<CraftingStation>();
                        CraftingLabor l = a.Go.GetComponent<CraftingLabor>();
                        if (st == null || l == null || cmd == null || BtcWorkbench.KeyOf(st) == 0u) return false;
                        Craft c = ParseCraft(p);
                        int max = BtcWorkbench.Max();
                        int n = 0;
                        for (int i = 0; i < c.Q.Count && n < max; i++)
                        {
                            RecipeInfo r = RecipeByName(c.Q[i]);
                            if (r == null) { BtcLog.Warn("BpPreset", "unknown recipe '" + c.Q[i] + "'"); continue; }
                            cmd.CmdcraftingQueueRecipe(a.Go, r.id, 1);
                            n++;
                        }
                        if (c.Loop) cmd.CmdcraftingLoop(a.Go, true);
                        if (c.Limit >= 0) BtcWorkbench.SetModeHost(st, false, c.Limit == 1);
                        if (c.Help >= 0 && BtcWorkbench.HelperStation(st)) BtcWorkbench.SetModeHost(st, true, c.Help == 1);
                        break;
                    }
                default: return true;
            }
            BtcLog.Info("BpPreset", "applied '" + p + "' to " + a.Go.name + " at " + a.Go.transform.position);
            return true;
        }

        private static uint NetIdOf(GameObject go)
        {
            NetworkIdentity ni = go != null ? go.GetComponent<NetworkIdentity>() : null;
            return ni != null ? ni.netId.Value : 0u;
        }

        // ------------------------------------------------------------------ compounds (local)
        private static string NodeKey(ValidationNode n)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return (n.Type != null ? n.Type.Name : "?") + "@" + n.Pos.x.ToString("F1", c) + "," + n.Pos.y.ToString("F1", c) + "," + n.Pos.z.ToString("F1", c);
        }

        /// BlueprintCompounder.GenerateBlueprintGraph postfix: node targets are still the source objects (live blueprints or preview copies)
        internal static void OnGraph(BlueprintGraph g)
        {
            if (g == null) return;
            OnGraphAnchor(g);
            OnGraphTasks(g);
            if (!On) return;
            Dictionary<string, string> m = null;
            foreach (ValidationNode n in g.Nodes)
            {
                string p = PayloadOfGo(n.Target);
                if (string.IsNullOrEmpty(p)) continue;
                if (m == null) m = new Dictionary<string, string>();
                m[NodeKey(n)] = p;
            }
            if (m != null) { ByGraph[g] = m; BtcLog.Info("BpPreset", "compound graph keeps " + m.Count + " preset(s)"); }
            else ByGraph.Remove(g);
        }

        /// BlueprintGraph.SpawnBlueprintsUnder postfix: preview copies get the presets of their nodes
        internal static void OnSpawnedUnder(BlueprintGraph g)
        {
            if (g == null) return;
            SpawnedUnderTasks(g);
            Dictionary<string, string> m;
            ByGraph.TryGetValue(g, out m);
            foreach (ValidationNode n in g.Nodes)
            {
                if (n.Target == null) continue;
                string p;
                if (m != null && m.TryGetValue(NodeKey(n), out p)) ByTarget[n.Target.GetInstanceID()] = p;
                else ByTarget.Remove(n.Target.GetInstanceID());
            }
        }

        /// BlueprintCompoundComponent.StartPlaceBlueprints prefix: remember where the presets go
        internal static void OnStartPlace(BlueprintCompoundComponent bcc)
        {
            OnStartPlaceTasks(bcc);
            if (bcc == null || bcc.Graph == null || !On) return;
            float now = Time.unscaledTime;
            int n = 0;
            foreach (ValidationNode node in bcc.Graph.Nodes)
            {
                if (node.Target == null || node.IsObstructed) continue;
                string p;
                if (!ByTarget.TryGetValue(node.Target.GetInstanceID(), out p) || string.IsNullOrEmpty(p)) continue;
                Pending.Add(new Pend { Pos = node.Target.transform.position, Name = node.Type != null ? node.Type.Name : null, Payload = p, T = now });
                n++;
            }
            OnPlaceDone(bcc);
            if (n > 0) BtcLog.Info("BpPreset", "placing a compound with " + n + " preset(s)");
        }

        /// a new blueprint appeared (host: UNetBlueprint._FinishSpawnBlueprint postfix, client: Blueprint.OnStartPureClient postfix)
        internal static void OnBlueprintSpawned(Blueprint b)
        {
            MatchTask(b);
            if (Pending.Count == 0 || b == null || !On) return;
            User u = User.LocalUser;
            if (u == null || !ReferenceEquals(b.faction, u.faction)) return;
            string name = NameOfAsset(b);
            Vector3 pos = b.transform.position;
            for (int i = 0; i < Pending.Count; i++)
            {
                Pend p = Pending[i];
                if (p.Name != name || (p.Pos - pos).sqrMagnitude > 0.04f) continue;
                Pending.RemoveAt(i);
                if (NetworkServer.active) { Store(KeyOf(b), p.Payload); BtcLog.Info("BpPreset", "compound preset on blueprint " + KeyOf(b) + " " + name); }
                else Set(b, p.Payload);
                return;
            }
        }

        /// BlueprintCompounder.SaveSelected postfix: presets into the compound folder
        internal static void OnCompoundSaved(Asset_Blueprints a)
        {
            if (a == null || a.Graph == null || string.IsNullOrEmpty(a.path)) return;
            try
            {
                string file = Path.Combine(a.path, CompoundFile);
                Dictionary<string, string> m;
                ByGraph.TryGetValue(a.Graph, out m);
                StringBuilder sb = new StringBuilder();
                AppendAnchor(a.Graph, sb);
                AppendTasks(a.Graph, sb);
                if (m != null) foreach (KeyValuePair<string, string> kv in m) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append('\n');
                if (sb.Length == 0) { if (File.Exists(file)) File.Delete(file); return; }
                File.WriteAllText(file, sb.ToString());
                BtcLog.Info("BpPreset", "compound '" + a.customName + "' saved with " + (m != null ? m.Count : 0) + " preset(s)" + (Anchors.ContainsKey(a.Graph) ? ", anchor" : ""));
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnCompoundSaved: " + e); }
        }

        /// BlueprintCompounder.LoadCompoundBlueprints prefix: presets of a saved compound (once per graph object)
        internal static void OnCompoundLoad(Asset_Blueprints a)
        {
            if (a == null || a.Graph == null || ByGraph.ContainsKey(a.Graph) || Anchors.ContainsKey(a.Graph) || TaskOfNode.ContainsKey(a.Graph) || string.IsNullOrEmpty(a.path)) return;
            try
            {
                string file = Path.Combine(a.path, CompoundFile);
                if (!File.Exists(file)) return;
                Dictionary<string, string> m = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(file))
                {
                    if (ReadAnchor(a.Graph, line) || ReadTasks(a.Graph, line)) continue;
                    int sp = line.IndexOf(' ');
                    if (sp > 0 && sp + 1 < line.Length) m[line.Substring(0, sp)] = line.Substring(sp + 1).Trim();
                }
                if (m.Count > 0) { ByGraph[a.Graph] = m; BtcLog.Info("BpPreset", "compound '" + a.customName + "' has " + m.Count + " preset(s)"); }
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnCompoundLoad: " + e); }
        }

        // ------------------------------------------------------------------ saves (host)
        private sealed class Saved { internal Vector3 Pos; internal string Name, Payload; }
        private static List<Saved> _pending;
        private static float _loadAt, _loadUntil;

        internal static void OnSave(string folder)
        {
            if (!NetworkServer.active || !On || string.IsNullOrEmpty(folder)) return;
            try
            {
                string file = Path.Combine(folder, FileName);
                CultureInfo c = CultureInfo.InvariantCulture;
                StringBuilder sb = new StringBuilder();
                int n = 0;
                foreach (KeyValuePair<uint, string> kv in Presets)
                {
                    NetworkIdentity ni;
                    if (!NetworkServer.objects.TryGetValue(new NetworkInstanceId(kv.Key), out ni) || ni == null) continue;
                    Blueprint b = ni.GetComponent<Blueprint>();
                    if (b == null) continue;
                    Vector3 p = b.transform.position;
                    sb.Append(p.x.ToString("F2", c)).Append(' ').Append(p.y.ToString("F2", c)).Append(' ').Append(p.z.ToString("F2", c)).Append(' ').Append(NameOfAsset(b)).Append(' ').Append(kv.Value).Append('\n');
                    n++;
                }
                if (n == 0) { if (File.Exists(file)) File.Delete(file); return; }
                File.WriteAllText(file, sb.ToString());
                BtcLog.Info("BpPreset", "saved " + n + " blueprint preset(s)");
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnSave: " + e); }
        }

        internal static void OnLoad(string folder)
        {
            _pending = null;
            if (!NetworkServer.active || !On || string.IsNullOrEmpty(folder)) return;
            try
            {
                string file = Path.Combine(folder, FileName);
                if (!File.Exists(file)) return;
                CultureInfo c = CultureInfo.InvariantCulture;
                List<Saved> list = new List<Saved>();
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] t = line.Split(' ');
                    float x, y, z;
                    if (t.Length == 5 && float.TryParse(t[0], NumberStyles.Float, c, out x) && float.TryParse(t[1], NumberStyles.Float, c, out y) && float.TryParse(t[2], NumberStyles.Float, c, out z))
                        list.Add(new Saved { Pos = new Vector3(x, y, z), Name = t[3], Payload = t[4] });
                }
                if (list.Count == 0) return;
                _pending = list;
                _loadAt = Time.unscaledTime + 2f;
                _loadUntil = Time.unscaledTime + 40f;
                BtcLog.Info("BpPreset", "save has " + list.Count + " blueprint preset(s), restoring after the load");
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnLoad: " + e); }
        }

        private static void LoadTick(float now)
        {
            if (now < _loadAt) return;
            _loadAt = now + 1f;
            Blueprint[] all = UnityEngine.Object.FindObjectsOfType<Blueprint>();   // once per second only while a save restores
            List<Saved> left = new List<Saved>();
            int done = 0;
            foreach (Saved s in _pending)
            {
                Blueprint best = null;
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && NameOfAsset(all[i]) == s.Name && (all[i].transform.position - s.Pos).sqrMagnitude < 0.25f && KeyOf(all[i]) != 0u) { best = all[i]; break; }
                if (best == null) { left.Add(s); continue; }
                Store(KeyOf(best), s.Payload);
                done++;
            }
            _pending = left.Count > 0 && now < _loadUntil ? left : null;
            if (_pending == null) BtcLog.Info("BpPreset", "restore finished, " + done + " this pass, " + left.Count + " not found");
        }

        // ------------------------------------------------------------------ hover summary (IMGUI, ResourcePackManager OnGUI)
        private static GUIStyle _style;
        private static readonly GUIContent _gc = new GUIContent();
        private static Blueprint _sumBp;
        private static string _sumSrc, _sumText;

        internal static void OnGUI()
        {
            if (!_marksBroken) try { DrawMarks(); } catch (Exception e) { _marksBroken = true; BtcLog.Error("BpPreset", "marks off after: " + e); }
            AnchorTick();
            if (Event.current.type == EventType.Repaint) try { DrawAnchor(); } catch (Exception e) { BtcLog.Error("BpPreset", "anchor draw: " + e.Message); _anchorBp = null; _placing = null; }
            if (Event.current.type != EventType.Repaint || !LocalOn) return;
            Blueprint b = Live(_hover);
            if (b == null || Time.unscaledTime - _hoverT > 30f) return;
            string p = Get(b);
            if (string.IsNullOrEmpty(p) || !ReferenceEquals(b.faction, User.LocalUser != null ? User.LocalUser.faction : null)) return;
            try
            {
                if (!ReferenceEquals(b, _sumBp) || p != _sumSrc) { _sumBp = b; _sumSrc = p; _sumText = Summary(b, p); }
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = false };
                    _style.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                    _style.padding = new RectOffset(8, 8, 4, 4);
                }
                _gc.text = _sumText;
                Vector2 sz = _style.CalcSize(_gc);
                Vector2 m = Event.current.mousePosition;
                Rect r = new Rect(Mathf.Min(m.x + 18f, Screen.width - sz.x - 4f), Mathf.Min(m.y + 22f, Screen.height - sz.y - 4f), sz.x, sz.y);
                _style.Draw(r, _gc, false, false, false, false);
            }
            catch (Exception e) { BtcLog.Error("BpPreset", "OnGUI: " + e.Message); _hover = null; }
        }

        private static string Summary(Blueprint b, string p)
        {
            bool ru = BtcLang.Russian;
            string head = ru ? "Преднастройка: " : "Preset: ";
            if (p[0] == 'S')
            {
                if (p == "S*") return head + (ru ? "принимать всё" : "accept everything");
                if (p == "S-") return head + (ru ? "держать пустым" : "keep empty");
                return head + (ru ? "только " : "only ") + ResName(p.Substring(2));
            }
            if (p[0] == 'R')
            {
                int kit;
                return head + (int.TryParse(p.Substring(1), out kit) && kit >= 0 && kit < BtcRackFilter.KitNames.Length ? (ru ? "только набор " : "only kit ") + BtcRackFilter.KitNames[kit] : "?");
            }
            Craft c = ParseCraft(p);
            int craft;
            Slot[] s = SlotsOf(KindOf(NameOfAsset(b), out craft) == KCraft ? craft : -1);
            StringBuilder sb = new StringBuilder(head);
            int[] cnt = new int[s.Length];
            for (int i = 0; i < c.Q.Count; i++)
                for (int j = 0; j < s.Length; j++)
                {
                    RecipeInfo[] inf = s[j].Infos();
                    if (inf.Length > 0 && inf[0] != null && inf[0].Creates != null && inf[0].Creates.Name == c.Q[i]) { cnt[j]++; break; }
                }
            bool any = false;
            for (int j = 0; j < s.Length; j++)
            {
                if (cnt[j] == 0) continue;
                sb.Append(any ? ", " : string.Empty).Append(ru ? s[j].Ru : s[j].En).Append(" x").Append(cnt[j]);
                any = true;
            }
            if (!any) sb.Append(ru ? "очередь пуста" : "empty queue");
            if (c.Loop) sb.Append(ru ? ", по кругу" : ", loop");
            if (c.Limit >= 0) sb.Append(ru ? (c.Limit == 1 ? ", лимит стоек вкл" : ", лимит стоек выкл") : (c.Limit == 1 ? ", rack limit on" : ", rack limit off"));
            if (c.Help >= 0) sb.Append(ru ? (c.Help == 1 ? ", помощник вкл" : ", помощник выкл") : (c.Help == 1 ? ", helper on" : ", helper off"));
            return sb.ToString();
        }

        private static string ResName(string code)
        {
            try
            {
                foreach (AdjectiveInfo a in AdjectiveInfo.GetInfosFromType(AdjectiveInfo.AdjectiveType.Ressource))
                    if (a.codeName == code) return string.IsNullOrEmpty(a.descriptiveName) ? code : a.descriptiveName;
            }
            catch { }
            return code;
        }

#if DEV
        internal static string DevState()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("presets=").Append(Presets.Count).Append(" graphs=").Append(ByGraph.Count).Append(" copies=").Append(ByTarget.Count)
              .Append(" pending=").Append(Pending.Count).Append(" applies=").Append(Applies.Count).Append(" localOn=").Append(LocalOn).Append(" server=").Append(NetworkServer.active);
            foreach (KeyValuePair<uint, string> kv in Presets) sb.Append(" [").Append(kv.Key).Append('=').Append(kv.Value).Append(']');
            return sb.ToString();
        }
#endif
    }
}
