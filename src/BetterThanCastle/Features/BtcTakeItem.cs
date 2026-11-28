using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Network;
using Brix.Game.Semantique;
using Brix.Legacy;
using Brix.UI.Icons;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// "Take item..." pie submenu: workers selected + hovering a crafting station or a stockpile shows every kind of item
    /// stored in it (icon, name, count); a click sends the selected workers to pick up exactly that kind (vanilla only
    /// offers the station's own choice). The order is the VANILLA command CmdPickup(worker, item): vanilla GoPickUpFrom ->
    /// GoPickUp(item) walks to the item's container and takes that item, so nothing new goes on the wire (BC OFF = vanilla).
    /// Pie text: 16 static slots BtcTake0..15 (Patches/LivrePatches.cs); PieContenu.GetButtonData postfix
    /// (Patches/TakeItemPatches.cs) puts the live name/icon/count of slot k into its button data.
    /// </summary>
    internal static class BtcTakeItem
    {
        internal const int Slots = 16;

        private sealed class Group
        {
            internal string Key;
            internal string Title;          // "##..." localisation key or plain text
            internal string IconName;       // IconKey name (pie icon) when known
            internal Sprite Sprite;
            internal readonly List<GameObject> Items = new List<GameObject>();
        }

        private static readonly List<Group> _groups = new List<Group>();
        private static readonly Dictionary<string, Group> _byKey = new Dictionary<string, Group>();
        private static readonly List<Labor> _labors = new List<Labor>();
        private static readonly List<GameObject> _tmp = new List<GameObject>();
        private static float _at = -1f;
        private static object _target;
        // what the open wheel shows (filled by FillButton): a click takes exactly the kind it shows even if the container changed since
        private static readonly Group[] _shown = new Group[Slots];
        private static readonly List<Labor> _shownLabors = new List<Labor>();

        private static string T(string en, string ru) { return BtcLang.GameRussian ? ru : en; }

        // ------------------------------------------------------------------ item names/icons
        private sealed class Known { internal string Title, Icon; }
        private static Dictionary<string, Known> _recipes;   // asset name of a crafted item -> vanilla recipe name + icon

        private static void R(Dictionary<string, Known> m, RecipeInfo r, string loc, string icon)
        {
            if (r == null || r.Creates == null || string.IsNullOrEmpty(r.Creates.Name) || m.ContainsKey(r.Creates.Name)) return;
            m[r.Creates.Name] = new Known { Title = "##recipe_" + loc, Icon = icon };
        }

        // Meta_Recipe.lua names + icons
        private static void EnsureRecipes()
        {
            if (_recipes != null) return;
            Dictionary<string, Known> m = new Dictionary<string, Known>();
            try
            {
                R(m, CookBook.Poleaxe, "poleaxe", "_Poleaxe"); R(m, CookBook.KettleHat, "kettlehat", "_Kettle_Hat");
                R(m, CookBook.Sword, "sword", "_Sword"); R(m, CookBook.Shield, "shield", "_Shield"); R(m, CookBook.Helmet, "helmet", "_Helmet");
                R(m, CookBook.Bow, "bow", "_Bow"); R(m, CookBook.Quiver, "quiver", "_Quiver"); R(m, CookBook.Cap, "cap", "_Cap");
                R(m, CookBook.CrossbowWeapon, "arbalistweapon", "_Crossbow"); R(m, CookBook.CrossbowPouch, "arbalistpouch", "_Pouch"); R(m, CookBook.CrossbowMorion, "arbalistcap", "_Kepi");
                R(m, CookBook.Staff, "artificerstaff", "_Staff"); R(m, CookBook.Sachel, "artificersatchel", "_Satchel"); R(m, CookBook.ArtificerHat, "artificerhat", "_Artificer_Hat");
                R(m, CookBook.AlchemistPouch, "alchemistpouch", "_Backpack"); R(m, CookBook.AlchemistHat, "alchemisthat", "_Bonnet");
                R(m, CookBook.Planks, "planks", "_Plank"); R(m, CookBook.IronIngot, "ironingot", "_Iron"); R(m, CookBook.GlassIngot, "glassingot", "_Glass_Ingot");
                R(m, CookBook.FabricPile, "fabricpile", "_Fabric"); R(m, CookBook.RopeBallot, "ropeballot", "_Rope"); R(m, CookBook.Cog, "cog", "_Cog");
                R(m, CookBook.Bomb, "bomb", "_Bomb"); R(m, CookBook.BearTrap, "beartrap", "_Beartrap"); R(m, CookBook.ExplosiveBarrel, "explosivebarrel", "_Explosive_Barrel");
                R(m, CookBook.SentinelWard, "sentinelward", "_Sentinel"); R(m, CookBook.HealingWard, "healingward", "_Healing_Ward"); R(m, CookBook.CaptureWard, "captureward", "_Capture_Stone");
                R(m, CookBook.LanternWard, "lanternward", "_Lantern"); R(m, CookBook.PylonWard, "pylonward", "_Pylon");
            }
            catch (Exception e) { BtcLog.Warn("TakeItem", "recipe names: " + e.Message); }
            _recipes = m;
        }

        private static Group NewGroup(GameObject go, string key)
        {
            Group g = new Group { Key = key };
            Known k;
            EnsureRecipes();
            if (_recipes.TryGetValue(key, out k)) { g.Title = k.Title; g.IconName = k.Icon; }
            else
            {
                try
                {
                    // raw and processed resources: the vanilla stockpile exclusion button of that resource
                    Ressource r = Apparence.PrincipaleOf(go);
                    AdjectiveKey ak = r != null ? r.GetKey() : null;
                    string code = ak != null ? ak.codeName : null;
                    if (code != null)
                    {
                        g.Title = Patches.BtcBpPie.NameOfCode(code);
                        g.IconName = Patches.BtcBpPie.IconOfCode(code);
                        if (g.IconName == null) { AdjectiveInfo info = ak.GetInfo(); if (info != null) g.Sprite = info.icon; }
                    }
                }
                catch (Exception) { }
            }
            if (string.IsNullOrEmpty(g.Title)) g.Title = key.Replace('_', ' ');
            if (g.Sprite == null && g.IconName != null) g.Sprite = BtcGhKit.Icon(g.IconName);
            if (g.Sprite == null) g.Sprite = BtcGhKit.Icon("_Take_From");
            return g;
        }

        // ------------------------------------------------------------------ snapshot of the hovered container
        private static Recepteur TargetRecepteur(out GameObject go)
        {
            go = Instructions.objet as GameObject;
            if (go == null) return null;
            if (go.GetComponent<Labor>() != null || go.GetComponentInParent<Blueprint>() != null) return null;
            CraftingStation st = go.GetComponent<CraftingStation>();
            if (st != null && !Own(go)) return null;   // vanilla crafting commands only work on own stations
            Recepteur r = go.GetComponent<Recepteur>();
            if (r != null) return r;
            GameComponent gc = go.GetComponent<GameComponent>();
            return gc != null ? gc.recepteur : null;
        }

        private static bool Own(GameObject go)
        {
            User u = User.LocalUser;
            GameComponent gc = go.GetComponent<GameComponent>();
            return u != null && u.faction != null && gc != null && gc.faction == u.faction;
        }

        private static void Refresh()
        {
            // pie conditions run every frame while the wheel is open: re-read the container at most 4 times a second
            if (ReferenceEquals(_target, Instructions.objet) && Time.unscaledTime - _at < 0.25f) return;
            _at = Time.unscaledTime;
            _target = Instructions.objet;
            _groups.Clear();
            _byKey.Clear();
            _labors.Clear();
            try
            {
                MonoBehaviour[] subj = Instructions.sujets;
                if (subj != null)
                    for (int i = 0; i < subj.Length; i++)
                    {
                        Labor l = subj[i] != null ? subj[i].GetComponent<Labor>() : null;
                        if (l != null && l.Profession != null && l.Profession.OccupationType == Occupation.Type.Builder) _labors.Add(l);
                    }
                if (_labors.Count == 0) return;
                GameObject target;
                Recepteur rec = TargetRecepteur(out target);
                if (rec == null) return;
                _tmp.Clear();
                foreach (IDescriptor d in rec.StoredItems) { GameObject o = d != null ? d.GameObject : null; if (o != null) _tmp.Add(o); }
                for (int i = 0; i < _tmp.Count; i++)
                {
                    GameObject o = _tmp[i];
                    GameComponent gc = o.GetComponent<GameComponent>();
                    string key = gc != null && gc.AssetKey != null ? gc.AssetKey.Name : o.name;
                    if (string.IsNullOrEmpty(key)) continue;
                    Group g;
                    if (!_byKey.TryGetValue(key, out g))
                    {
                        if (_groups.Count >= Slots) continue;
                        g = NewGroup(o, key);
                        _byKey[key] = g;
                        _groups.Add(g);
                    }
                    g.Items.Add(o);
                }
                _tmp.Clear();
            }
            catch (Exception e) { _groups.Clear(); BtcLog.Warn("TakeItem", "snapshot: " + e.Message); }
        }

        private static bool Visible() { Refresh(); return _groups.Count > 0; }

        private static bool Has(int k) { Refresh(); return k < _groups.Count; }

        private static bool CanTake(int k)
        {
            Refresh();
            if (k >= _groups.Count) return false;
            GameObject item = _groups[k].Items[0];
            for (int i = 0; i < _labors.Count; i++) if (_labors[i] != null && _labors[i].recepteur != null && _labors[i].recepteur.HasRoomFor(item)) return true;
            return false;
        }

        /// <summary>Pie button of slot k: each selected worker goes for its own item of that kind (cycles when there are fewer).</summary>
        private static void Take(int k)
        {
            try
            {
                Group g = k >= 0 && k < Slots ? _shown[k] : null;
                if (g == null || g.Items.Count == 0) return;
                List<Labor> labors = _shownLabors;
                User u = User.LocalUser;
                UNetProjectCmd cmd = u != null ? u.GetComponent<UNetProjectCmd>() : null;
                if (cmd == null) return;
                int n = 0;
                for (int i = 0; i < labors.Count; i++)
                {
                    Labor l = labors[i];
                    if (l == null || l.recepteur == null) continue;
                    GameObject item = g.Items[n % g.Items.Count];
                    if (item == null || !l.recepteur.HasRoomFor(item)) continue;
                    n++;
                    l.AssignToIdle();
                    if (item.GetComponent<NetworkIdentity>() != null) cmd.CallCmdPickup(l.gameObject, item);   // vanilla command (recorded in replays)
                    else if (NetworkServer.active) Order.Transfer.GoPickUp.Send(l, Smooth.Algebraics.Tuple.Create<GameObject, int>(item, -1), Labor.DecisionSource.User);
                    else BtcLog.Warn("TakeItem", "item " + g.Key + " has no network id, client cannot name it");
                }
                if (n > 0) BtcLog.Info("TakeItem", n + " worker(s) take " + g.Key + " (" + g.Items.Count + " stored)");
            }
            catch (Exception e) { BtcLog.Error("TakeItem", "take: " + e); }
        }

        /// <summary>PieContenu.GetButtonData postfix: live title, icon and count of the BtcTake slots.</summary>
        internal static void FillButton(string nom, Hashtable h)
        {
            if (h == null || nom == null || !nom.StartsWith("BtcTake", StringComparison.Ordinal)) return;
            int k;
            if (nom.Length < 8 || !int.TryParse(nom.Substring(7), out k)) return;
            Refresh();
            if (k < 0 || k >= _groups.Count || k >= Slots) return;
            Group g = _groups[k];
            _shown[k] = g;
            if (k == 0) { _shownLabors.Clear(); _shownLabors.AddRange(_labors); }
            // count goes into the title: a non-empty checkLabel makes the vanilla pie use its number item (craft queue +/-),
            // which does not close the wheel on click (ghost pie + dead mouse, user test 2026-11-28)
            string title = g.Title;
            if (title != null && title.StartsWith("##", StringComparison.Ordinal)) { try { string loc = Brix.Utils.I2Helper.TryGet(title); if (!string.IsNullOrEmpty(loc)) title = loc; } catch (Exception) { } }
            h["titre"] = g.Items.Count > 1 ? title + " x" + g.Items.Count : title;
            if (g.Sprite != null) h["sprite"] = g.Sprite;
            h["tooltip"] = T("Selected workers go and take this item from here", "Выделенные рабочие идут и берут отсюда именно этот предмет");
        }

        internal static void Install()
        {
            Instructions.conditions["Condition_BtcTake_Visible"] = () => Visible();
            for (int i = 0; i < Slots; i++)
            {
                int k = i;
                Instructions.conditions["Condition_BtcTake_Has" + k] = () => Has(k);
                Instructions.conditions["Condition_BtcTake_Can" + k] = () => CanTake(k);
                Instructions.commendes["Event_BtcTake_" + k] = () => Take(k);
            }
        }

        // ------------------------------------------------------------------ pie DSL (injected by Patches/LivrePatches.cs)
        internal static string Buttons()
        {
            StringBuilder b = new StringBuilder(4096);
            b.Append("\n\nBUTTON BtcTakeHub\n\tName : ").Append(T("Take item...", "Взять предмет..."))
             .Append("\n\tCondition : Condition_BtcTake_Visible\n\tIcon : _Take_From\n\tTooltip : ")
             .Append(T("Choose which stored item the selected workers go and take", "Выбрать, какой именно лежащий здесь предмет пойдут брать выделенные рабочие")).Append('\n');
            for (int i = 0; i < Slots; i++)
                b.Append("\nBUTTON BtcTake").Append(i).Append("\n\tName : ").Append(T("Item", "Предмет"))
                 .Append("\n\tEffect : Event_BtcTake_").Append(i).Append("\n\tCondition : Condition_BtcTake_Can").Append(i)
                 .Append("\n\tIcon : _Take_From\n\tTooltip : ").Append(T("Take this item", "Взять этот предмет")).Append('\n');
            return b.ToString();
        }

        /// <summary>The pie slot that replaces a hole: the hub with one entry per stored kind, or the hole when nothing is stored.</summary>
        internal static string MenuSlot()
        {
            StringBuilder b = new StringBuilder(1024);
            b.Append("\tIf: Condition_BtcTake_Visible\n\t\tSubmenu: BtcTakeHub\n");
            for (int i = 0; i < Slots; i++) b.Append("\t\t\tIf: Condition_BtcTake_Has").Append(i).Append("\n\t\t\t\tButton: BtcTake").Append(i).Append("\n\t\t\tEnd\n");
            b.Append("\t\tEnd\n\tElse:\n\t\tHole\n\tEnd\n");
            return b.ToString();
        }
    }
}
