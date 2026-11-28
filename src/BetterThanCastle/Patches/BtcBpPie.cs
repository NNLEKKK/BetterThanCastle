using System;
using System.Collections.Generic;
using System.Text;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// Pie menu DSL of advanced blueprints (BtcBpPreset), appended to Info/Livre text by BtcLivre.Read (game files stay untouched).
    /// Hovering an own blue mark of a stockpile / rack / station opens MENU BtcBpStock / BtcBpRack / BtcBpCraft0..7 (hover tags answered by
    /// BpPresetTestHoverPatch), both with and without the build tool. Stockpile "only X" buttons are clones of the vanilla exclusion buttons.
    /// </summary>
    internal static class BtcBpPie
    {
        private static string T(string en, string ru) { return BtcLang.Russian ? ru : en; }

        private static void Btn(StringBuilder b, string id, string name, string effect, string cond, string icon, string tip)
        {
            b.Append("\nBUTTON ").Append(id).Append("\n\tName : ").Append(name).Append("\n\tEffect : ").Append(effect)
             .Append("\n\tCondition : ").Append(cond).Append("\n\tIcon : ").Append(icon).Append("\n\tTooltip : ").Append(tip).Append('\n');
        }

        private static void Hub(StringBuilder b, string id, string name, string cond, string icon, string tip)
        {
            b.Append("\nBUTTON ").Append(id).Append("\n\tName : ").Append(name).Append("\n\tCondition : ").Append(cond)
             .Append("\n\tIcon : ").Append(icon).Append("\n\tTooltip : ").Append(tip).Append('\n');
        }

        private sealed class Excl { internal string Id, Name, Code, Icon, Tip; }

        /// vanilla "BUTTON ExclusionX" blocks (name, resource code, icon, tooltip)
        private static List<Excl> Exclusions(string vanilla)
        {
            List<Excl> l = new List<Excl>();
            int at = 0;
            while ((at = vanilla.IndexOf("BUTTON Exclusion", at, StringComparison.Ordinal)) >= 0)
            {
                int end = vanilla.IndexOf("BUTTON ", at + 7, StringComparison.Ordinal);
                string block = vanilla.Substring(at, (end < 0 ? vanilla.Length : end) - at);
                at += 7;
                Excl e = new Excl();
                foreach (string raw in block.Split('\n'))
                {
                    string s = raw.Trim();
                    int c = s.IndexOf(':');
                    if (s.StartsWith("BUTTON ")) { e.Id = s.Substring(7).Trim(); continue; }
                    if (c < 0) continue;
                    string k = s.Substring(0, c).Trim(), v = s.Substring(c + 1).Trim();
                    if (k == "Name") e.Name = v;
                    else if (k == "Icon") e.Icon = v;
                    else if (k == "Tooltip") e.Tip = v;
                    else if (k == "Effect" && v.StartsWith("Event_Stockpile_Exclude_")) e.Code = v.Substring("Event_Stockpile_Exclude_".Length);
                }
                if (!string.IsNullOrEmpty(e.Id) && !string.IsNullOrEmpty(e.Code)) l.Add(e);
            }
            return l;
        }

        private static List<Excl> _excl;

        /// vanilla pie icon of a stockpile resource code ("WoodBlock" -> "_Log"), null if unknown
        internal static string IconOfCode(string code)
        {
            if (_excl != null) for (int i = 0; i < _excl.Count; i++) if (_excl[i].Code == code) return _excl[i].Icon;
            return null;
        }

        internal static string Buttons(string vanilla)
        {
            StringBuilder b = new StringBuilder(12000);
            b.Append('\n');
            Btn(b, "BtcBpClear", T("Clear preset", "Сбросить преднастройку"), "Event_BtcBp_Clear", "Condition_BtcBp_Has", "UI_Reset",
                T("The building gets vanilla settings when it is built", "Здание получит обычные настройки при постройке"));
            Btn(b, "BtcBpCopy", T("Copy preset", "Копировать преднастройку"), "Event_BtcBp_Copy", "Condition_BtcBp_Has", "_Take_From",
                T("Remember this preset to paste it onto other marks of the same kind", "Запомнить преднастройку, чтобы вставить её на другие такие же метки"));
            Btn(b, "BtcBpPaste", T("Paste preset", "Вставить преднастройку"), "Event_BtcBp_Paste", "Condition_BtcBp_CanPaste", "_Drop_Into",
                T("Give this mark the copied preset", "Дать этой метке скопированную преднастройку"));

            // stockpile
            Btn(b, "BtcBpStAll", T("Will accept everything", "Будет принимать всё"), "Event_BtcBp_StAll", "Condition_BtcBp_NotAll", "_Stockpiled_Brick",
                T("When built, this stockpile accepts every resource", "После постройки склад принимает любые ресурсы"));
            Btn(b, "BtcBpStNone", T("Will be kept empty", "Будет пустым"), "Event_BtcBp_StNone", "Condition_BtcBp_NotNone", "_Stockpile_Empty",
                T("When built, this stockpile accepts nothing", "После постройки склад ничего не принимает"));
            Hub(b, "BtcBpStOnlyHub", T("Will accept only...", "Будет принимать только..."), "Condition_BtcBp_Any", "MultiWood",
                T("When built, this stockpile accepts only the chosen resource", "После постройки склад принимает только выбранный ресурс"));
            _excl = Exclusions(vanilla);
            for (int i = 0; i < _excl.Count; i++)
            {
                Excl e = _excl[i];
                Btn(b, "BtcBpOnly" + e.Id.Substring("Exclusion".Length), e.Name ?? e.Code, "Event_BtcBp_Only_" + e.Code, "Condition_BtcBp_NotOnly_" + e.Code, e.Icon ?? "MultiWood", e.Tip ?? e.Code);
            }
            if (_excl.Count == 0) BtcLog.Error("Livre", "no vanilla stockpile exclusion buttons found, blueprint stockpile presets have no resource list");

            // rack
            string[] ru = { "Рыцарь", "Лучник", "Алебардист", "Арбалетчик", "Алхимик", "Артифисер" };
            Btn(b, "BtcBpRackAny", T("Will take any kit", "Любой набор"), "Event_BtcBp_Rack_Any", "Condition_BtcBp_RackNot_Any", "UI_Check",
                T("When built, the rack has no kit filter (vanilla)", "После постройки у стойки нет фильтра наборов (как в оригинале)"));
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++)
            {
                string k = BtcRackFilter.KitNames[i];
                Btn(b, "BtcBpRack" + k, T("Will hold only " + k.ToLowerInvariant() + " kits", "Только " + ru[i].ToLowerInvariant()), "Event_BtcBp_Rack_" + k, "Condition_BtcBp_RackNot_" + k,
                    BtcRackFilter.KitIcons[i], T("When built, only " + k.ToLowerInvariant() + " kits can be stored on this rack", "После постройки на стойку можно класть только набор - " + ru[i].ToLowerInvariant()));
            }

            // stations
            for (int k = 0; k < BtcBpPreset.StationNames.Length; k++)
            {
                Hub(b, "BtcBpAdd" + k, T("Add to the queue", "Добавить в очередь"), "Condition_BtcBp_CanAdd", BtcBpPreset.StationIcons[k],
                    T("Orders the station starts with when it is built (up to 40)", "Заказы, с которыми мастерская начнёт работу после постройки (до 40)"));
                BtcBpPreset.Slot[] s = BtcBpPreset.SlotsOf(k);
                for (int i = 0; i < s.Length; i++)
                    Btn(b, "BtcBpQ" + k + "_" + i, T("Queue " + s[i].En.ToLowerInvariant(), "В очередь - " + s[i].Ru.ToLowerInvariant()), "Event_BtcBp_Q" + k + "_" + i, "Condition_BtcBp_Q" + k + "_" + i, s[i].Icon,
                        T("Add one " + s[i].En.ToLowerInvariant() + " to the preset queue (a kit adds all its parts)", "Добавить в очередь преднастройки - " + s[i].Ru.ToLowerInvariant() + " (набор добавляет все свои части)"));
            }
            Btn(b, "BtcBpLoopOn", T("Loop on", "Включить повтор по кругу"), "Event_BtcBp_Loop", "Condition_BtcBp_NoLoop", "Gear",
                T("When built, the station repeats its queue", "После постройки мастерская повторяет очередь по кругу"));
            Btn(b, "BtcBpLoopOff", T("Loop off", "Выключить повтор по кругу"), "Event_BtcBp_Loop", "Condition_BtcBp_Loop", "UI_X",
                T("The station crafts the queue once", "Мастерская сделает очередь один раз"));
            Btn(b, "BtcBpOpen", T("Station panel", "Панель мастерской"), "Event_BtcBp_Open", "Condition_BtcBp_Any", "_Workbench",
                T("Opens the panel of this station mark (like a built station): kits, single parts, the whole queue, loop", "Открывает панель этой метки мастерской (как у готовой): наборы, отдельные части, вся очередь, повтор"));
            Btn(b, "BtcBpRemoveLast", T("Remove the last order", "Убрать последний заказ"), "Event_BtcBp_RemoveLast", "Condition_BtcBp_HasQueue", "UI_Trash",
                T("Removes the last added order (a whole kit) from the preset queue", "Убирает последний добавленный заказ (весь набор) из очереди преднастройки"));
            Btn(b, "BtcBpLimitOn", T("Rack limit on", "Включить лимит стоек"), "Event_BtcBp_Limit", "Condition_BtcBp_LimitOff", "_Tool_Rack",
                T("Better Workbenches - the station skips kit parts no rack has room for", "Better Workbenches - мастерская пропускает части наборов, для которых нет места на стойках"));
            Btn(b, "BtcBpLimitOff", T("Rack limit off", "Выключить лимит стоек"), "Event_BtcBp_Limit", "Condition_BtcBp_LimitOn", "UI_X",
                T("Better Workbenches - the station crafts even when racks are full", "Better Workbenches - мастерская крафтит, даже если стойки заполнены"));
            Btn(b, "BtcBpHelpOn", T("Helper on", "Включить помощника"), "Event_BtcBp_Help", "Condition_BtcBp_HelpOff", "_Operator",
                T("Better Workbenches - one more worker brings what the next orders need", "Better Workbenches - ещё один рабочий подносит то, что нужно следующим заказам"));
            Btn(b, "BtcBpHelpOff", T("Helper off", "Выключить помощника"), "Event_BtcBp_Help", "Condition_BtcBp_HelpOn", "UI_X",
                T("Better Workbenches - no helper for this station", "Better Workbenches - без помощника у этой мастерской"));
            return b.ToString();
        }

        /// one slot: "off" button while on, "on" button while off, else a hole
        private static string Pair(string condOn, string btnOff, string condOff, string btnOn)
        {
            return "\tIf: " + condOn + "\n\t\tButton: " + btnOff + "\n\tElse:\n\t\tIf: " + condOff + "\n\t\t\tButton: " + btnOn
                 + "\n\t\tElse:\n\t\t\tHole\n\t\tEnd\n\tEnd\n";
        }

        private static string HoverChain(string ind)
        {
            StringBuilder b = new StringBuilder();
            b.Append(ind).Append("Hover: BtcBpStock\n").Append(ind).Append("\tMenu: BtcBpStock\n");
            b.Append(ind).Append("Else: BtcBpRack\n").Append(ind).Append("\tMenu: BtcBpRack\n");
            for (int k = 0; k < BtcBpPreset.StationNames.Length; k++)
                b.Append(ind).Append("Else: BtcBpCraft").Append(k).Append('\n').Append(ind).Append("\tMenu: BtcBpCraft").Append(k).Append('\n');
            return b.ToString();
        }

        private static string StockHub(string text)
        {
            int m = text.IndexOf("MENU PaletteAnywhere", StringComparison.Ordinal);
            string head = "\tSubmenu: StockpileExclusivityHub\n";
            int a = m < 0 ? -1 : text.IndexOf(head, m, StringComparison.Ordinal);
            int e = a < 0 ? -1 : text.IndexOf("\n\tEnd\n", a, StringComparison.Ordinal);
            if (e > a && a > 0) return text.Substring(a + head.Length, e + 1 - (a + head.Length)).Replace("Button: Exclusion", "Button: BtcBpOnly");
            StringBuilder b = new StringBuilder();   // fallback: plain list
            if (_excl != null) for (int i = 0; i < _excl.Count; i++) b.Append("\t\tButton: BtcBpOnly").Append(_excl[i].Id.Substring("Exclusion".Length)).Append('\n');
            return b.ToString();
        }

        internal static string Menus(string text)
        {
            StringBuilder m = new StringBuilder(8000);
            m.Append("\nMENU BtcBpStock\n\tButton: BtcBpStAll\n\tButton: BtcBpStNone\n\tSubmenu: BtcBpStOnlyHub\n").Append(StockHub(text))
             .Append("\tEnd\n\tButton: BtcBpClear\n\tHole\n\tButton: BtcBpCopy\n\tButton: BtcBpPaste\n\tHole\n");
            m.Append("\nMENU BtcBpRack\n\tButton: BtcBpRackAny\n");
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++) m.Append("\tButton: BtcBpRack").Append(BtcRackFilter.KitNames[i]).Append('\n');
            m.Append("\tHole\n");
            for (int k = 0; k < BtcBpPreset.StationNames.Length; k++)
            {
                m.Append("\nMENU BtcBpCraft").Append(k).Append("\n\tSubmenu: BtcBpAdd").Append(k).Append('\n');
                BtcBpPreset.Slot[] s = BtcBpPreset.SlotsOf(k);
                for (int i = 0; i < s.Length; i++) m.Append("\t\tButton: BtcBpQ").Append(k).Append('_').Append(i).Append('\n');
                m.Append("\tEnd\n").Append(Pair("Condition_BtcBp_Loop", "BtcBpLoopOff", "Condition_BtcBp_NoLoop", "BtcBpLoopOn"))
                 .Append("\tButton: BtcBpOpen\n\tButton: BtcBpClear\n")
                 .Append(Pair("Condition_BtcBp_LimitOn", "BtcBpLimitOff", "Condition_BtcBp_LimitOff", "BtcBpLimitOn"))
                 .Append(Pair("Condition_BtcBp_HelpOn", "BtcBpHelpOff", "Condition_BtcBp_HelpOff", "BtcBpHelpOn"))
                 .Append("\tButton: BtcBpCopy\n\tButton: BtcBpPaste\n");
            }

            // hover branches inside MENU Game: without the build tool (front of the hover chain) and with it (around the Sel chain)
            int g = text.IndexOf("MENU Game", StringComparison.Ordinal);
            string plain = "\t\t\tHover: Catapult\n";
            int h = g < 0 ? -1 : text.IndexOf(plain, g, StringComparison.Ordinal);
            if (h < 0) BtcLog.Error("Livre", "MENU Game hover chain anchor not found, blueprint presets not reachable without the build tool");
            else text = text.Substring(0, h) + HoverChain("\t\t\t") + "\t\t\tElse: Catapult\n" + text.Substring(h + plain.Length);

            string tool = "\t\tIf: Condition_OutilConstruction\n\t\t\tSel: Blocs\n\t\t\t\tMenu: Blocks\n\t\t\tElse: Structures\n\t\t\t\tMenu: Structures\n\t\t\tEnd\n";
            int t = g < 0 ? -1 : text.IndexOf(tool, g, StringComparison.Ordinal);
            if (t < 0) BtcLog.Error("Livre", "MENU Game build tool anchor not found, blueprint presets not reachable with the build tool");
            else
            {
                string repl = "\t\tIf: Condition_OutilConstruction\n" + HoverChain("\t\t\t") + "\t\t\tElse:\n\t\t\t\tSel: Blocs\n\t\t\t\t\tMenu: Blocks\n\t\t\t\tElse: Structures\n"
                            + "\t\t\t\t\tMenu: Structures\n\t\t\t\tEnd\n\t\t\tEnd\n";
                text = text.Substring(0, t) + repl + text.Substring(t + tool.Length);
            }
            BtcLog.Info("Livre", "blueprint preset menus injected (" + (_excl != null ? _excl.Count : 0) + " stockpile resources)");
            return text + m;
        }
    }
}
