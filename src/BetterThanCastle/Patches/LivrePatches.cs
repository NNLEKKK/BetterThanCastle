using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Legacy;
using HarmonyLib;

namespace BetterThanCastle.Patches
{
    /// <summary>Livre.Lire(patron, emplacement): File.ReadAllText(path) -> BtcLivre.Read(path) so the mod can append pie menu DSL (game files stay untouched).</summary>
    [HarmonyPatch(typeof(Livre), "Lire", new[] { typeof(string), typeof(string) })]
    internal static class LivreLirePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
            MethodInfo from = AccessTools.Method(typeof(File), "ReadAllText", new[] { typeof(string) });
            MethodInfo to = AccessTools.Method(typeof(BtcLivre), "Read", new[] { typeof(string) });
            int n = 0;
            for (int i = 0; i < ins.Count; i++)
            {
                if ((ins[i].operand as MethodInfo) == from)
                {
                    ins[i].operand = to;
                    n++;
                }
            }
            if (n == 0) BtcLog.Error("Hook", "Livre.Lire: File.ReadAllText not found, rack filter menu not injected");
            return ins;
        }
    }

    public static class BtcLivre
    {
        public static string Read(string path)
        {
            string text = File.ReadAllText(path);
            try
            {
                string p = path != null ? path.Replace((char)92, '/') : string.Empty;
                bool crlf = text.IndexOf((char)13) >= 0;
                if (p.EndsWith("/PieMenu_Buttons.txt"))
                {
                    string n = text.Replace("\r\n", "\n");
                    return Back(n + RackButtons() + WormButtons() + BtcBpPie.Buttons(n), crlf);
                }
                if (p.EndsWith("/PieMenu_Menus.txt")) return Back(BtcBpPie.Menus(PatchWormMenus(PatchMenus(text.Replace("\r\n", "\n")))), crlf);
            }
            catch (Exception e) { BtcLog.Error("Livre", "inject failed: " + e); }
            return text;
        }

        private static string Back(string s, bool crlf) { return crlf ? s.Replace("\n", "\r\n") : s; }

        private static string T(string en, string ru) { return BtcLang.GameRussian ? ru : en; }

        private static string RackButtons()
        {
            string[] ru = { "Рыцарь", "Лучник", "Алебардист", "Арбалетчик", "Алхимик", "Геомант" };
            StringBuilder b = new StringBuilder(2048);
            b.Append("\n\nBUTTON RackKitHub\n\tName : ").Append(T("Rack filter", "Фильтр стойки"))
             .Append("\n\tCondition : Condition_Rack_Filter_Visible\n\tIcon : _Knight_Kit\n\tTooltip : ")
             .Append(T("Allow only one kit on this rack (kits already on it stay until taken)", "Разрешить на стойке только один набор (лежащие наборы остаются, пока их не заберут)")).Append('\n');
            b.Append("\nBUTTON RackKitAny\n\tName : ").Append(T("Any kit", "Любой набор"))
             .Append("\n\tEffect : Event_Rack_Filter_Any\n\tCondition : Condition_Rack_Filter_Not_Any\n\tIcon : UI_Check\n\tTooltip : ")
             .Append(T("No filter (vanilla behaviour)", "Без фильтра (как в оригинале)")).Append('\n');
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++)
            {
                string k = BtcRackFilter.KitNames[i];
                b.Append("\nBUTTON RackKit").Append(k).Append("\n\tName : ").Append(T(k + " kit only", ru[i] + ": только этот набор"))
                 .Append("\n\tEffect : Event_Rack_Filter_").Append(k).Append("\n\tCondition : Condition_Rack_Filter_Not_").Append(k)
                 .Append("\n\tIcon : ").Append(BtcRackFilter.KitIcons[i]).Append("\n\tTooltip : ")
                 .Append(T("Only " + k.ToLowerInvariant() + " kits can be stored on this rack", "На стойку можно класть только набор: " + ru[i])).Append('\n');
            }
            return b.ToString();
        }

        // ------------------------------------------------------------------ wormhole wards (BtcWormWard, BtcWormWardCraft)
        private static void Btn(StringBuilder b, string id, string name, string effect, string cond, string icon, string tip)
        {
            b.Append("\nBUTTON ").Append(id).Append("\n\tName : ").Append(name).Append("\n\tEffect : ").Append(effect)
             .Append("\n\tCondition : ").Append(cond).Append("\n\tIcon : ").Append(icon).Append("\n\tTooltip : ").Append(tip).Append('\n');
        }

        private static string WormButtons()
        {
            StringBuilder b = new StringBuilder(1024);
            b.Append('\n');
            Btn(b, "Queue Wormhole Ward", T("Wormhole Ward", "Вард-червоточина"), "Event_Workbench_OrderWormWard", "Condition_Worm_Ward_Craft", BtcWormWardLook.IconName,
                T("A white ward that never shoots: own wormhole wards and crystals are instant travel points for phantoms", "Белый вард, который не стреляет: свои варды-червоточины и кристаллы становятся для фантомов точками мгновенного перемещения"));
            Btn(b, "WormWardOff", T("Teleport: off", "Выключить телепорт"), "Event_Worm_Ward_Toggle", "Condition_Worm_Ward_On", "UI_X",
                T("Phantoms ignore this wormhole ward until you switch it on again", "Фантомы не используют этот вард-червоточину, пока вы снова его не включите"));
            Btn(b, "WormWardOn", T("Teleport: on", "Включить телепорт"), "Event_Worm_Ward_Toggle", "Condition_Worm_Ward_Off", "UI_Check",
                T("Phantoms may travel through this wormhole ward again", "Фантомы снова могут перемещаться через этот вард-червоточину"));
            Btn(b, "WormNestOff", T("Teleport: off", "Выключить телепорт"), "Event_Worm_Nest_Toggle", "Condition_Worm_Nest_On", "UI_X",
                T("Phantoms do not use this crystal as a wormhole until you switch it on again", "Фантомы не используют этот кристалл как портал, пока вы снова его не включите"));
            Btn(b, "WormNestOn", T("Teleport: on", "Включить телепорт"), "Event_Worm_Nest_Toggle", "Condition_Worm_Nest_Off", "UI_Check",
                T("Phantoms may use this crystal as a wormhole again", "Фантомы снова могут использовать этот кристалл как портал"));
            return b.ToString();
        }

        /// <summary>One pie slot that shows the "off" button while the point is on, the "on" button while it is off, else a hole.</summary>
        private static string Slot(string condOn, string btnOff, string condOff, string btnOn, string ind)
        {
            return ind + "If: " + condOn + "\n" + ind + "\tButton: " + btnOff + "\n" + ind + "Else:\n" + ind + "\tIf: " + condOff + "\n" + ind + "\t\tButton: " + btnOn
                 + "\n" + ind + "\tElse:\n" + ind + "\t\tHole\n" + ind + "\tEnd\n" + ind + "End\n";
        }

        /// <summary>Replace the first "Hole" line after the anchor inside the named menu.</summary>
        private static string ReplaceHole(string text, string menu, string anchor, string with, string what)
        {
            int m = text.IndexOf("MENU " + menu + "\n", StringComparison.Ordinal);
            if (m < 0) m = text.IndexOf("MENU " + menu, StringComparison.Ordinal);
            int a = m < 0 ? -1 : text.IndexOf(anchor, m, StringComparison.Ordinal);
            int h = a < 0 ? -1 : text.IndexOf("\tHole\n", a + anchor.Length, StringComparison.Ordinal);
            int next = m < 0 ? -1 : text.IndexOf("\nMENU ", m + 5, StringComparison.Ordinal);
            if (h < 0 || (next >= 0 && h > next))
            {
                BtcLog.Error("Livre", "MENU " + menu + " anchor not found, " + what + " not injected");
                return text;
            }
            return text.Substring(0, h) + with + text.Substring(h + "\tHole\n".Length);
        }

        private static string PatchWormMenus(string text)
        {
            text = ReplaceHole(text, "Altar", "Queue Geomancer Kit", "\tIf: Condition_Worm_Ward_Craft\n\t\tButton: Queue Wormhole Ward\n\tElse:\n\t\tHole\n\tEnd\n", "altar wormhole ward");
            text = ReplaceHole(text, "BuilderWard", "Break Object", Slot("Condition_Worm_Ward_On", "WormWardOff", "Condition_Worm_Ward_Off", "WormWardOn", "\t"), "wormhole ward switch");
            text = ReplaceHole(text, "Crystal", "Call To Arms", Slot("Condition_Worm_Nest_On", "WormNestOff", "Condition_Worm_Nest_Off", "WormNestOn", "\t"), "crystal wormhole switch");
            return text;
        }

        private static string PatchMenus(string text)
        {
            int m = text.IndexOf("MENU BuilderToolRack", StringComparison.Ordinal);
            string anchor = "Button: StoreInContainer\n\tHole\n";
            int a = m < 0 ? -1 : text.IndexOf(anchor, m, StringComparison.Ordinal);
            if (a < 0 || a - m > 1500)
            {
                BtcLog.Error("Livre", "MENU BuilderToolRack anchor not found, rack filter submenu not injected");
                return text;
            }
            StringBuilder b = new StringBuilder();
            b.Append("Button: StoreInContainer\n\tIf: Condition_Rack_Filter_Visible\n\t\tSubmenu: RackKitHub\n\t\t\tButton: RackKitAny\n");
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++) b.Append("\t\t\tButton: RackKit").Append(BtcRackFilter.KitNames[i]).Append('\n');
            b.Append("\t\tEnd\n\tElse:\n\t\tHole\n\tEnd\n");
            text = text.Substring(0, a) + b + text.Substring(a + anchor.Length);

            // nothing selected, hovering an own rack: the pie is the kit filter itself (vanilla shows New Task there)
            string hover = "\t\t\tElse: ToolRack\n\t\t\t\tMenu: New Task\n";
            int h = text.IndexOf(hover, StringComparison.Ordinal);
            if (h < 0)
            {
                BtcLog.Error("Livre", "MENU Game hover ToolRack anchor not found, rack filter on hover not injected");
                return text;
            }
            string repl = "\t\t\tElse: ToolRack\n\t\t\t\tIf: Condition_Rack_Filter_Visible\n\t\t\t\t\tMenu: RackKitHover\n\t\t\t\tElse:\n\t\t\t\t\tMenu: New Task\n\t\t\t\tEnd\n";
            StringBuilder hm = new StringBuilder("\nMENU RackKitHover\n\tButton: RackKitAny\n");
            for (int i = 0; i < BtcRackFilter.KitNames.Length; i++) hm.Append("\tButton: RackKit").Append(BtcRackFilter.KitNames[i]).Append('\n');
            hm.Append("\tHole\n");
            return text.Substring(0, h) + repl + text.Substring(h + hover.Length) + hm;
        }
    }
}
